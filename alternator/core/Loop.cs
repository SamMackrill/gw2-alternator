namespace guildwars2.tools.alternator;

static class Loop
{
    public interface ILoop
    {
        /// <summary>
        /// Waits for all threads to finish
        /// </summary>
        void Wait();

        object? Result { get; }
    }

    public interface IState
    {
        /// <summary>
        /// Breaks out of the loop, termination all threads after completing their current task and returning the value
        /// </summary>
        void Return(object? o);
    }

    public delegate void WorkAction(byte thread, long index, IState state);

    private class ParallelFor : IState, ILoop
    {
        private class Worker
        {
            private readonly ParallelFor source;
            private long index;
            private long last;
            private readonly byte threadIndex;
            private bool completed;
            private Thread? thread;

            public Worker(ParallelFor p, byte index)
            {
                source = p;
                threadIndex = index;
            }

            private void DoWork()
            {
                while (true)
                {
                    lock (source)
                    {
                        if (source.from < source.to && !source.abort)
                        {
                            index = source.from++;
                        }
                        else
                        {
                            lock (this)
                            {
                                thread = null;
                                completed = true; 
                                source.active--;
                                return;
                            }
                        }
                    }

                    try
                    {
                        source.work(threadIndex, index, source);
                    }
                    catch (Exception)
                    {
                        //Util.Logging.Log(e);
                    }
                }
            }

            public void Start()
            {
                lock (this)
                {
                    if (thread == null || !thread.IsAlive)
                    {
                        completed = false;
                        thread = new Thread(DoWork)
                        {
                            IsBackground = true
                        };
                        thread.Start();
                    }
                }
            }

            private void Abort()
            {
                lock (this)
                {
                    var t = thread;

                    if (t is not {IsAlive: true}) return;

                    thread = null;
                    completed = true;
                }
            }

            public void Restart()
            {
                lock (this)
                {
                    Abort();
                    Start();
                }
            }

            public bool Wait(int millis)
            {
                if (thread == null) return true;
                if (millis > 0)
                {
                    return thread.Join(millis);
                }

                thread.Join();
                return true;
            }

            public bool IsComplete => completed;

            public bool IsBlocked()
            {
                if (completed)
                    return false;

                var i = index;

                if (i == last)
                {
                    return true;
                }

                last = i;

                return false;
            }
        }

        private long from;
        private readonly long to;
        private readonly int timeout;
        private readonly WorkAction work;
        private readonly Worker[] threads;
        private byte active;
        private bool abort;

        public ParallelFor(long from, long to, byte threads, int timeout, WorkAction work)
        {
            this.from = from;
            this.to = to;
            this.threads = new Worker[threads];
            this.timeout = timeout;
            this.work = work;
            active = threads;

            lock (this)
            {
                for (byte i = 0; i < threads; i++)
                {
                    var t = this.threads[i] = new Worker(this, i);
                    t.Start();
                }
            }
        }

        private Worker? FindActive()
        {
            return active == 0 ? null : threads.FirstOrDefault(t => !t.IsComplete);
        }

        public void Wait()
        {
            while (true)
            {
                var activeWorker = FindActive();
                if (activeWorker == null) break;

                if (activeWorker.Wait(timeout)) continue;

                foreach (var t in threads)
                {
                    if (!t.IsBlocked()) continue;
                    lock (this)
                    {
                        if (!abort)
                        {
                            t.Restart();
                        }
                    }
                }
            }
        }
        public void Return(object? o)
        {
            lock (this)
            {
                if (abort)
                    return;
                abort = true;
                Result = o;
            }
        }

        public object? Result { get; private set; }
    }

    /// <summary>
    /// Loops through the items using multiple threads
    /// </summary>
    /// <param name="from">Start index, inclusive</param>
    /// <param name="to">End index, exclusive</param>
    /// <param name="threads">Number of threads to use</param>
    /// <param name="timeout">The duration a task can run for before being aborted, if > 0</param>
    /// <param name="work">Work to run</param>
    public static ILoop For(long from, long to, byte threads, int timeout, WorkAction work)
    {
        return new ParallelFor(from, to, threads, timeout, work);
    }
}