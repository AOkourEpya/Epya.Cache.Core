using Epya.Cache.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Client
{
    /// <summary>
    /// ASP.NET Core replacement for the former System.Web.Http action filter.
    /// The read path (serve a cached GET and short-circuit the action) runs as an action
    /// filter. The write path (capture the rendered JSON and push it to the cache) runs as
    /// a result filter, because in ASP.NET Core the response body only exists once the
    /// action result has executed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CacheAttribute : Attribute, IAsyncActionFilter, IAsyncResultFilter
    {
        private const string ServedFromCacheKey = "__Epya.Cache.ServedFromCache";

        // Headers replayed out of the cache must not fight the ones the server computes
        // for the live response, or the connection framing breaks.
        private static readonly HashSet<string> NonReplayableHeaders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Content-Length", "Content-Type", "Transfer-Encoding",
                "Connection", "Keep-Alive", "Upgrade", "Date", "Server"
            };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            HttpContext httpContext = context.HttpContext;

            // An attribute is instantiated by the runtime, not the container, so the
            // configuration is resolved per request out of RequestServices.
            CacheConfigDto configs = httpContext.RequestServices.GetCacheConfigs();

            // Apply caching only for GET requests
            if (!HttpMethods.IsGet(httpContext.Request.Method) || configs == null || !configs.IsEnabledAndConfigured)
            {
                await next();
                return;
            }

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                CacheManager mgr = httpContext.RequestServices.GetService<CacheManager>() ?? new CacheManager(configs);

                CacheRequest request = new CacheRequest
                {
                    FullUrl = httpContext.Request.GetDisplayUrl(),
                    UserName = httpContext.Items["Username"] as string ?? "Unknown",
                    Action = Common.Action.Read
                };

                CacheResponse cacheResponse = await mgr.SendReadRequestAsync(request).ConfigureAwait(false);
                long responsetime = stopwatch.ElapsedMilliseconds;

                if (httpContext.RequestAborted.IsCancellationRequested)
                {
                    context.Result = new ContentResult
                    {
                        StatusCode = StatusCodes.Status500InternalServerError,
                        Content = "Request was canceled"
                    };
                    _LogToFile(configs, "Request was canceled");
                    return;
                }

                if (cacheResponse != null && cacheResponse.ResponseCode == "200")
                {
                    byte[] responseBody = Encoding.UTF8.GetBytes((string)cacheResponse.Body);

                    IHeaderDictionary headers = httpContext.Response.Headers;
                    _AddOrAppendHeader(headers, "X-Cache-Header", "true");
                    _AddOrAppendHeader(headers, "access-control-expose-headers", "X-Cache-Header,X-Updated-1");

                    if (cacheResponse.Headers != null)
                    {
                        foreach (KeyValuePair<string, string> h in cacheResponse.Headers)
                        {
                            _AddOrAppendHeader(headers, h.Key, h.Value);
                        }
                    }

                    // Tells the result filter below not to write this body straight back
                    // into the cache it just came out of.
                    httpContext.Items[ServedFromCacheKey] = true;

                    // FileContentResult writes the exact byte count, so Content-Length is set for us.
                    context.Result = new FileContentResult(responseBody, "application/json");

                    long readResponsetime = stopwatch.ElapsedMilliseconds - responsetime;
                    stopwatch.Stop();

                    // Log without blocking
                    int headerCount = cacheResponse.Headers == null ? 0 : cacheResponse.Headers.Count;
                    _ = Task.Run(() =>
                        _LogToFile(configs, $"[Cache] Total time : {stopwatch.Elapsed.Milliseconds / 1000} seconds, " +
                                  $"Response time: {responsetime / 1000.0} seconds & read time : {readResponsetime / 1000.0} seconds, " +
                                  $"header count: {headerCount} " +
                                  $"for URL: {request.FullUrl}"));

                    return;
                }
            }
            catch (OperationCanceledException)
            {
                context.Result = new ContentResult { StatusCode = 499, Content = "Request was canceled" };
                return;
            }
            catch (Exception ex)
            {
                context.Result = new ContentResult
                {
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Content = $"Error: {ex.Message} + {ex.StackTrace}"
                };
                _LogToFile(configs, ex.Message);
                return;
            }

            await next();
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            HttpContext httpContext = context.HttpContext;

            // Apply caching only for GET requests, and never re-cache a body that was
            // itself served from the cache.
            if (!HttpMethods.IsGet(httpContext.Request.Method) ||
                httpContext.Items.ContainsKey(ServedFromCacheKey))
            {
                await next();
                return;
            }

            Stream originalBody = httpContext.Response.Body;
            MemoryStream buffer = new MemoryStream();
            string responseBody = null;

            try
            {
                httpContext.Response.Body = buffer;
                await next();
            }
            finally
            {
                httpContext.Response.Body = originalBody;

                if (buffer.Length > 0)
                {
                    buffer.Position = 0;
                    using (StreamReader reader = new StreamReader(buffer, Encoding.UTF8, false, 1024, true))
                    {
                        responseBody = await reader.ReadToEndAsync();
                    }

                    // Hand the buffered body on to the real response stream.
                    buffer.Position = 0;
                    await buffer.CopyToAsync(originalBody);
                }

                buffer.Dispose();
            }

            try
            {
                if (CommonOperations.IsValidJsonAndHasT(responseBody))
                {
                    CacheManager mgr = httpContext.RequestServices.GetService<CacheManager>()
                        ?? new CacheManager(httpContext.RequestServices.GetCacheConfigs());

                    CacheRequest request = new CacheRequest();
                    request.FullUrl = httpContext.Request.GetDisplayUrl();
                    request.UserName = httpContext.Items["Username"] as string ?? "Unknown";
                    request.Body = responseBody;
                    request.Action = Common.Action.Write;
                    request.Headers = new Dictionary<string, string>();

                    foreach (KeyValuePair<string, StringValues> header in httpContext.Response.Headers)
                    {
                        request.Headers[header.Key] = header.Value.ToString();
                    }

                    // Fire-and-forget without blocking execution
                    _ = Task.Run(async () => await mgr.SendWriteRequestAsync(request).ConfigureAwait(false));
                }
            }
            catch
            {

            }
        }



        private static readonly object fileLock = new object();
        private void _AddOrAppendHeader(IHeaderDictionary headers, string headerKey, string headerValue)
        {
            if (NonReplayableHeaders.Contains(headerKey))
                return;

            if (headers.TryGetValue(headerKey, out StringValues existingValues))
            {
                // Append new value alongside the existing ones
                headers[headerKey] = StringValues.Concat(existingValues, headerValue);
            }
            else
            {
                // Add new header if it does not exist
                headers[headerKey] = headerValue;
            }
        }
        private void _LogToFile(CacheConfigDto configs, string message)
        {
            if (configs == null || !configs.IsLoggingEnabled)
                return;

            string logMessage = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {message}";

            try
            {
                lock (fileLock)
                {
                    using (StreamWriter writer = File.AppendText(configs.LogFilePath))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle logging error (optional)
                Trace.WriteLine($"[Log Error] Failed to write to log file: {ex.Message}");
            }
        }

    }

}
