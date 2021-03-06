using System;
using System.Threading;
using System.Threading.Tasks;
using STM.Implementation.Lockbased;

namespace STMTester
{
    public class DiningPhilosopher
    {
        private static readonly int MAX_EAT_COUNT = 50;
        private static TMInt eatCounter = new TMInt(0);

        public void Start()
        {
            var fork1 = new Fork();
            var fork2 = new Fork();
            var fork3 = new Fork();
            var fork4 = new Fork();
            var fork5 = new Fork();

            var t1 = StartPhilosopher(fork1, fork2);
            var t2 = StartPhilosopher(fork2, fork3);
            var t3 = StartPhilosopher(fork3, fork4);
            var t4 = StartPhilosopher(fork4, fork5);
            var t5 = StartPhilosopher(fork5, fork1);

            Task.WaitAll(t1, t2, t3, t4, t5);
        }

        private Task StartPhilosopher(Fork left, Fork right)
        {
            var t1 = new Task(() =>
            {
                while (eatCounter < MAX_EAT_COUNT)
                {
                    STMSystem.Atomic(() =>
                    {
                        left.AttemptToPickUp();
                        right.AttemptToPickUp();
                    });

                    Console.WriteLine("Thread: " + Thread.CurrentThread.ManagedThreadId + " eating.");
                    Thread.Sleep(100);
                    Console.WriteLine("Eat count: " + ++eatCounter);

                    STMSystem.Atomic(() =>
                    {
                        left.State.Value = true;
                        right.State.Value = true;
                    });

                    Thread.Sleep(100);
                }
            });

            t1.Start();

            return t1;
        }

        public class Fork
        {
            public TMVar<bool> State { get; set; }

            public Fork()
            {
                State = new TMVar<bool>(true);
            }

            public void AttemptToPickUp()
            {
                STMSystem.Atomic(() =>
                {
                    if (!State.Value)
                    {
                        STMSystem.Retry();
                    }
                    State.Value = false;
                });
            }
        }
    }
}