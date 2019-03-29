using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using System.Collections.Concurrent;
using Vms;

namespace RouterServer
{
    class Program
    {
        static void Main(string[] args)
        {

            Server server = new Server
            {
                Services = { NvrService.BindService(new NvrServiceImpl()) },
                Ports = { new ServerPort("0.0.0.0", 7777, ServerCredentials.Insecure) }
            };
            server.Start();
            System.Console.WriteLine("Server Listen: 0.0.0.0:7777");
            Thread.Sleep(Timeout.Infinite);
        }
    }

    class NvrServiceImpl : Vms.NvrService.NvrServiceBase
    {
        Object locker = new object();
        ConcurrentQueue<StreamFrame> frameList = new ConcurrentQueue<StreamFrame>();

        bool isOnline = false;

        private void addFrame(StreamFrame f)
        {
            lock (locker)
            {
                // if (frameList.Count > 2000)
                //     frameList.TryDequeue(out _);
                frameList.Enqueue(f);
                //System.Console.WriteLine(System.DateTime.Now + " ==> New frame");
            }
        }

        private StreamFrame getFrame()
        {
            if (frameList.Count <= 0 && isOnline)
            {
                System.Console.WriteLine(System.DateTime.Now + " ==> waiting new frame");
                Thread.Sleep(500);
            }

            StreamFrame f;
            frameList.TryDequeue(out f);
            return f;
        }

        double fps = 0.0;
        public override async Task<Dummy> PushCameraStream(IAsyncStreamReader<StreamFrame> requestStream, ServerCallContext context)
        {
            System.Console.WriteLine("PushCameraStream Connected: " + context.Peer);

            isOnline = true;
            int count = 0;
            long ts = DateTime.Now.Ticks;
            while (await requestStream.MoveNext())
            {
                addFrame(requestStream.Current);
                count++;
                if (new TimeSpan(DateTime.Now.Ticks - ts).TotalMilliseconds >= 2000)
                {
                    ts = DateTime.Now.Ticks;
                    fps = count / 2.0;
                    count = 0;
                    System.Console.WriteLine("Q size: " + frameList.Count + ", FPS: " + fps);
                }
            }
            isOnline = false;

            context.Status = Status.DefaultSuccess;
            return new Dummy();
        }

        public override async Task GetCameraStream(IAsyncStreamReader<CameraStreamQ> requestStream, IServerStreamWriter<StreamFrame> responseStream, ServerCallContext context)
        {
            System.Console.WriteLine("GetCameraStream Connected: " + context.Host);

            await requestStream.MoveNext();
            if (!isOnline)
            {
                context.Status = new Status(StatusCode.Cancelled, "Not online stream");
                return;
            }

            long ts = DateTime.Now.Ticks;
            while (isOnline)
            {
                while (new TimeSpan(DateTime.Now.Ticks - ts).TotalMilliseconds < (1000 / fps))
                    Thread.Sleep(5);
                await responseStream.WriteAsync(getFrame());
            }

            context.Status = Status.DefaultSuccess;
        }
    }
}
