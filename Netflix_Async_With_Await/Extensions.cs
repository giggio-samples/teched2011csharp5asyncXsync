using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CS_Netflix_WPF_AsyncWithAwait
{

    public static class Ext
    {
        public static Task<string> DownloadStringTaskAsync(this WebClient webClient, Uri address, CancellationToken cancellationToken)
        {
            return webClient.DownloadStringTaskAsync(address, cancellationToken, null);
        }


        public static Task<string> DownloadStringTaskAsync(this WebClient webClient, Uri address, CancellationToken cancellationToken, IProgress<DownloadProgressChangedEventArgs> progress)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>(address);
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
            }
            else
            {
                DownloadProgressChangedEventHandler handler = null;
                CancellationTokenRegistration ctr = cancellationToken.Register(webClient.CancelAsync);
                DownloadStringCompletedEventHandler completedHandler = null;
                DownloadProgressChangedEventHandler progressHandler = null;
                if (progress != null)
                {
                    if (handler == null)
                    {
                        handler = delegate(object s, DownloadProgressChangedEventArgs e)
                        {
                            EAPCommon.HandleProgress<string, DownloadProgressChangedEventArgs>(tcs, e, () => e, progress);
                        };
                    }
                    progressHandler = handler;
                }
                completedHandler = delegate(object sender, DownloadStringCompletedEventArgs e)
                {
                    EAPCommon.HandleCompletion<string>(tcs, true, e, () => e.Result, delegate
                    {
                        ctr.Dispose();
                        webClient.DownloadProgressChanged -= progressHandler;
                        webClient.DownloadStringCompleted -= completedHandler;
                    });
                };
                webClient.DownloadProgressChanged += progressHandler;
                webClient.DownloadStringCompleted += completedHandler;
                try
                {
                    webClient.DownloadStringAsync(address, tcs);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        webClient.CancelAsync();
                    }
                }
                catch
                {
                    webClient.DownloadProgressChanged -= progressHandler;
                    webClient.DownloadStringCompleted -= completedHandler;
                    throw;
                }
            }
            return tcs.Task;
        }
    }

    internal static class EAPCommon
    {
        internal static void HandleCompletion<T>(TaskCompletionSource<T> tcs, bool requireMatch, AsyncCompletedEventArgs e, Func<T> getResult, Action unregisterHandler)
        {
            if (!requireMatch || (e.UserState == tcs))
            {
                try
                {
                    unregisterHandler();
                }
                finally
                {
                    if (e.Cancelled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else if (e.Error != null)
                    {
                        tcs.TrySetException(e.Error);
                    }
                    else
                    {
                        tcs.TrySetResult(getResult());
                    }
                }
            }
        }

        internal static void HandleProgress<T, E>(TaskCompletionSource<T> tcs, ProgressChangedEventArgs eventArgs, Func<E> getProgress, IProgress<E> callback)
        {
            if (eventArgs.UserState == tcs)
            {
                callback.Report(getProgress());
            }
        }
    }
}
