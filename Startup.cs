using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;

namespace WebSocketSample
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder => {
                builder.AddConsole()
                    .AddDebug()
                    .AddFilter<ConsoleLoggerProvider>(category: null, level: LogLevel.Debug)
                    .AddFilter<DebugLoggerProvider>(category: null, level:LogLevel.Debug);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            var socketOpt = new WebSocketOptions{
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024                
            };            

            app.UseWebSockets(socketOpt);

            app.Use(async (context, next) =>
            {
                //await context.Response.WriteAsync("Hello World!");
                await next();
            });

            app.Use(async(context, next) => 
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket websocket = await context.WebSockets.AcceptWebSocketAsync();      
                        await Response(context, websocket);
                    }
                    else
                    {
                        await context.Response.WriteAsync("Request is not webs");                        
                    }
                }
                else
                {
                    await next();
                }
            });
        }

        private async Task Response(HttpContext context, WebSocket websocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);            

            Debug.WriteLine($"Received info: {System.Text.ASCIIEncoding.UTF8.GetString(buffer)}");

            while(!result.CloseStatus.HasValue)
            {
                var currentContent = System.Text.ASCIIEncoding.UTF8.GetString(buffer).TrimEnd('\0');
                
                var serverContent = System.Text.ASCIIEncoding.UTF8.GetBytes(currentContent + "+++Content from server+++");
                var arraySegment = new ArraySegment<byte>(serverContent, 0, serverContent.Length);
                
                await websocket.SendAsync(arraySegment, result.MessageType, result.EndOfMessage, CancellationToken.None);
                result = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);                
            }
            await websocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }
}
