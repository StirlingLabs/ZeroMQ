using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ZeroMQ;

namespace Examples
{
    namespace FLCliApi
    {
        //
        // flcliapi class - Freelance Pattern agent class
        // Implements the Freelance Protocol at http://rfc.zeromq.org/spec:10
        //
        // Author: metadings
        //

        // This API works in two halves, a common pattern for APIs that need to
        // run in the background. One half is an frontend object our application
        // creates and works with; the other half is a backend "agent" that runs
        // in a background thread. The frontend talks to the backend over an
        // inproc pipe socket:

        public class FreelanceClient : IDisposable
        {
            // Here we see the backend agent. It runs as an attached thread, talking
            // to its parent over a pipe socket. It is a fairly complex piece of work
            // so we'll break it down into pieces.

            // Our context
            ZContext context;

            // Pipe through to flcliapi agent
            public ZActor Actor { get; protected set; }

            public FreelanceClient()
            {
                // Constructor
                context = new();

                Actor = new(context, Agent);
                Actor.Start();
            }

            ~FreelanceClient()
                => Dispose(false);

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                Dispose(true);
            }

            protected void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Destructor

                    if (Actor != null)
                    {
                        Actor.Dispose();
                        Actor = null;
                    }
                    if (context != null)
                    {
                        // Do context.Dispose()

                        context.Dispose();
                        context = null;
                    }
                }
            }

            public void Connect(string endpoint)
            {
                // To implement the connect method, the frontend object sends a multipart
                // message to the backend agent. The first part is a string "CONNECT", and
                // the second part is the endpoint. It waits 100msec for the connection to
                // come up, which isn't pretty, but saves us from sending all requests to a
                // single server, at startup time:

                using (var message = ZMessage.Create())
                {
                    message.Add(new("CONNECT"));
                    message.Add(new(endpoint));

                    Actor.Frontend.Send(message);
                }

                Thread.Sleep(64); // Allow connection to come up
            }

            public ZMessage? Request(ZMessage? request)
            {
                // To implement the request method, the frontend object sends a message
                // to the backend, specifying a command "REQUEST" and the request message:

                request.Prepend(new("REQUEST"));

                Actor.Frontend.Send(request);

                ZMessage? reply;
                if (null != (reply = Actor.Frontend.ReceiveMessage(out var error)))
                {
                    var status = reply.PopString();
                    if (status == "FAILED")
                    {
                        reply.Dispose();
                        reply = null;
                    }
                }
                return reply;
            }

            public static void Agent(ZContext context, ZSocket backend, CancellationTokenSource canceller, object[] args)
            {
                // Finally, here's the agent task itself, which polls its two sockets
                // and processes incoming messages:

                using (var agent = new Agent(context, backend))
                {
                    var p = ZPollItem.CreateReceiver();

                    while (!canceller.IsCancellationRequested)
                    {

                        // Poll the control message

                        if (agent.Pipe.PollIn(p, out var msg, out var error, TimeSpan.FromMilliseconds(64)))
                            using (msg)
                                agent.ControlMessage(msg);
                        else
                        {
                            if (error == ZError.ETERM)
                                break; // Interrupted
                            if (error != ZError.EAGAIN)
                                throw new ZException(error);
                        }

                        // Poll the router message

                        if (agent.Router.PollIn(p, out msg, out error, TimeSpan.FromMilliseconds(64)))
                            using (msg)
                                agent.RouterMessage(msg);
                        else
                        {
                            if (error == ZError.ETERM)
                                break; // Interrupted
                            if (error != ZError.EAGAIN)
                                throw new ZException(error);
                        }

                        if (agent.Request != null)
                        {
                            // If we're processing a request, dispatch to next server

                            if (DateTime.UtcNow >= agent.Expires)
                            {
                                // Request expired, kill it
                                using (var outgoing = new ZFrame("FAILED"))
                                    agent.Pipe.Send(outgoing);

                                agent.Request.Dispose();
                                agent.Request = null;
                            }
                            else
                                // Find server to talk to, remove any expired ones
                                foreach (var server in agent.Actives.ToList())
                                {
                                    if (DateTime.UtcNow >= server.Expires)
                                    {
                                        agent.Actives.Remove(server);
                                        server.Alive = false;
                                    }
                                    else
                                        // Copy the Request, Push the Endpoint and send on Router
                                        using (var request = agent.Request.Clone())
                                        {
                                            request.Prepend(new(server.Endpoint));

                                            agent.Router.Send(request);
                                            break;
                                        }
                                }
                        }

                        // Disconnect and delete any expired servers
                        // Send heartbeats to idle servers if needed
                        foreach (var server in agent.Servers)
                            server.Ping(agent.Router);
                    }
                }
            }
        }

        public class Agent : IDisposable
        {
            static readonly TimeSpan GLOBAL_TIMEOUT = TimeSpan.FromMilliseconds(3000);

            // We build the agent as a class that's capable of processing messages
            // coming in from its various sockets:

            // Simple class for one background agent

            // Socket to talk back to application
            public ZSocket Pipe;

            // Socket to talk to servers
            public ZSocket Router;

            // Servers we've connected to
            public HashSet<Server> Servers;

            // Servers we know are alive
            public List<Server> Actives;

            // Number of requests ever sent
            int sequence;

            // Current request if any
            public ZMessage? Request;

            // Current reply if any
            // ZMessage reply;

            // Timeout for request/reply
            public DateTime Expires;

            public Agent(ZContext context, ZSocket pipe)
                : this(context, default, pipe)
            {
                var rnd = new Random();
                Router.IdentityString = "CLIENT" + rnd.Next();
            }

            public Agent(ZContext context, string name, ZSocket pipe)
            {
                // Constructor
                Pipe = pipe;

                Router = new(context, ZSocketType.ROUTER);
                if (name != null)
                    Router.IdentityString = name;

                Servers = new();
                Actives = new();
            }

            ~Agent()
                => Dispose(false);

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                Dispose(true);
            }

            protected void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Destructor

                    Servers = null;
                    Actives = null;

                    if (Request != null)
                    {
                        Request.Dispose();
                        Request = null;
                    }
                    /* if (this.reply != null)
                    {
                      this.reply.Dispose();
                      this.reply = null;
                    } */

                    if (Router != null)
                    {
                        Router.Dispose();
                        Router = null;
                    }
                }
            }

            public void ControlMessage(ZMessage? msg)
            {
                // This method processes one message from our frontend class
                // (it's going to be CONNECT or REQUEST):

                var command = msg.PopString();

                if (command == "CONNECT")
                {
                    var endpoint = msg.PopString();
                    Console.WriteLine("I: connecting to {0}...", endpoint);

                    Router.Connect(endpoint);

                    var server = new Server(endpoint);
                    Servers.Add(server);
                    Actives.Add(server);
                }
                else if (command == "REQUEST")
                {
                    if (Request != null)
                        // Strict request-reply cycle
                        throw new InvalidOperationException();

                    // Prefix request with sequence number and empty envelope
                    msg.Prepend(new(++sequence));

                    // Take ownership of request message
                    Request = msg.Clone();

                    // Request expires after global timeout
                    Expires = DateTime.UtcNow + GLOBAL_TIMEOUT;
                }
            }

            public void RouterMessage(ZMessage? reply)
            {
                // This method processes one message from a connected
                // server:

                // Frame 0 is server that replied
                var endpoint = reply.PopString();
                var server = Servers.Single(s => s.Endpoint == endpoint);
                if (!server.Alive)
                {
                    Actives.Add(server);
                    server.Refresh(true);
                }

                // Frame 1 may be sequence number for reply
                reply.TryPop<int>(out var seq);

                if (seq != sequence)
                    return;

                reply.Prepend(new("OK"));

                Pipe.Send(reply);

                Request.Dispose();
                Request = null;
            }
        }

        public class Server
        {
            public static readonly TimeSpan PING_INTERVAL = TimeSpan.FromMilliseconds(2000);

            public static readonly TimeSpan SERVER_TTL = TimeSpan.FromMilliseconds(6000);

            // Simple class for one server we talk to

            // Server identity/endpoint
            public string Endpoint { get; protected set; }

            // true if known to be alive
            public bool Alive { get; set; }

            // Next ping at this time
            public DateTime PingAt { get; protected set; }

            // Expires at this time
            public DateTime Expires { get; protected set; }

            public Server(string endpoint)
            {
                Endpoint = endpoint;
                Refresh(true);
            }

            public void Refresh(bool alive)
            {
                Alive = alive;

                if (!alive) return;

                PingAt = DateTime.UtcNow + PING_INTERVAL;
                Expires = DateTime.UtcNow + SERVER_TTL;
            }

            public void Ping(ZSocket socket)
            {
                if (DateTime.UtcNow < PingAt) return;

                using (var outgoing = ZMessage.Create())
                {
                    outgoing.Add(new(Endpoint));
                    outgoing.Add(new("PING"));

                    socket.Send(outgoing);
                }

                PingAt = DateTime.UtcNow + PING_INTERVAL;
            }

            public override int GetHashCode()
                => Endpoint.GetHashCode();
        }
    }
}
