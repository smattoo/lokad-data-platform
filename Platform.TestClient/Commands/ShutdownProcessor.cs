﻿using System;
using System.Threading;
using Platform.Messages;
using ServiceStack.ServiceClient.Web;

namespace Platform.TestClient.Commands
{
    public class ShutdownProcessor : ICommandProcessor
    {
        public string Key { get { return "SHUTDOWN"; } }
        public string Usage { get { return @"SHUTDOWN
    Sends immediate shutdown request to the server."; } }
        public bool Execute(CommandProcessorContext context, CancellationToken token, string[] args)
        {
            try
            {
                var result =
                    new JsonServiceClient(context.Client.ClientHttpBase).Get<ClientDto.ShutdownServerResponse>(
                        "/system/shutdown/");


                return result.Success;
            }
            catch(Exception ex)
            {
                context.Log.Info("Failed to get response. Server might be already down.");
            }
            return true;
        }
    }
}