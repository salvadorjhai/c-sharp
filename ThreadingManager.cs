using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

public class ThreadingManager : IDisposable
{
    public void Dispose()
    {
        try
        {
            if (Threadlimiter != null)
                Threadlimiter.Release(MAX_THREAD);
        }
        catch (Exception) { }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    int active_thread_count = 0;

    int MAX_THREAD = 1;

    List<Task> taskList;
    Semaphore Threadlimiter;

    private bool iswaiting = false;

    public ThreadingManager(int maxthread)
    {
        MAX_THREAD = maxthread;

        if (MAX_THREAD>1)
        {
            Threadlimiter = new Semaphore(0, MAX_THREAD);
            Threadlimiter.Release(MAX_THREAD);

            taskList = new List<Task>();
        }

        active_thread_count = 0;
        iswaiting = false;

    }

    public Task NewAction(Action A)
    {
        if (MAX_THREAD > 1)
        {
            // Limiter
            try
            {
                if (Threadlimiter != null)
                    Threadlimiter.WaitOne();
            }
            catch (Exception ex) { }
            
            // create thread        
            Task t = new Task(() => StartThread(A), TaskCreationOptions.PreferFairness);
            t.Start();
            taskList.Add(t);
            return t;

        } else
        {
            StartThread(A);
        }
        return null;
    }
  
    private void StartThread(Action A)
    {
        try
        {
            // increase avtive threads
            Interlocked.Increment(ref active_thread_count);
            // invoke action
            A.Invoke();
        }
        catch (Exception) { }
        finally
        {
            // release 1 on semaphore
            if (Threadlimiter != null) { Threadlimiter.Release(1); }

            // decrease active threads
            Interlocked.Decrement(ref active_thread_count);
            GC.Collect();
        }
    }

    /// <summary>
    /// wait for all thread to complete
    /// </summary>
    /// <param name="timeout"></param>
    public void WaitAllThreads(int timeout = 60000)
    {
        // if single thread, return
        if (active_thread_count == 0) { return; }
        if (taskList == null) { return; }

        // if already waiting, return
        if (iswaiting) { return; }

        // wait for all task to within given timeout
        try
        {
            Task.WaitAll(taskList.ToArray(), timeout);
        }
        catch (Exception) { }

        // release all semaphore
        // clear task list
        try
        {
            if (Threadlimiter != null)
                Threadlimiter.Release(MAX_THREAD);
        }
        catch (Exception) { }
        finally
        {
            taskList.Clear();
            GC.Collect();
        }

    }

}