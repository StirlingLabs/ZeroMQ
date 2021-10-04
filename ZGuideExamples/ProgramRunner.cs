using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;
using ZeroMQ.lib;

namespace Examples
{
    static class ProgramRunner
    {
        static ProgramRunner()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
                var ex = (Exception)args.ExceptionObject;

                var w = Console.Error;

                w.WriteLine("UnhandledException");
                w.WriteLine(ex.GetType().AssemblyQualifiedName);
                w.WriteLine(ex.ToString());

                var i = 0;
                var iex = ex;
                while ((iex = iex.InnerException) is not null)
                {
                    w.WriteLine($"== INNER EXCEPTION {i} ==");
                    w.WriteLine(ex.GetType().AssemblyQualifiedName);
                    w.WriteLine(ex.ToString());
                }

                w.Flush();
            };
            TaskScheduler.UnobservedTaskException += (sender, args) => {
                var ex = (Exception)args.Exception;

                var w = Console.Error;

                w.WriteLine("UnhandledException");
                w.WriteLine(ex.GetType().AssemblyQualifiedName);
                w.WriteLine(ex.ToString());

                var i = 0;
                var iex = ex;
                while ((iex = iex.InnerException) is not null)
                {
                    w.WriteLine($"== INNER EXCEPTION {i} ==");
                    w.WriteLine(ex.GetType().AssemblyQualifiedName);
                    w.WriteLine(ex.ToString());
                }

                w.Flush();
            };
        }

        static int Main(string[] args)
        {   
            var returnMain = 0; // C good

            var leaveOut = 0;
            var dict = new Dictionary<string, string>();
            var fields = typeof(Program).GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(field => field.Name)
                .ToArray();

            foreach (var arg in args)
            {
                if (!arg.StartsWith("--", StringComparison.OrdinalIgnoreCase)) break;

                leaveOut++;

                var iOfEquals = arg.IndexOf('=');
                string key, value;
                if (-1 < iOfEquals)
                {
                    key = arg.Substring(0, iOfEquals);
                    value = arg.Substring(iOfEquals + 1);
                }
                else
                {
                    key = arg.Substring(0);
                    value = null;
                }
                dict.Add(key, value);

                var keyField = fields
                    .FirstOrDefault(field => string.Equals(field.Name, key.Substring(2), StringComparison.OrdinalIgnoreCase));

                if (keyField == null)
                    continue;

                if (keyField.FieldType == typeof(string))
                    keyField.SetValue(null, value);

                else if (keyField.FieldType == typeof(bool))
                {
                    var equalsTrue = string.IsNullOrEmpty(value);
                    if (!equalsTrue)
                        equalsTrue = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    if (!equalsTrue)
                        equalsTrue = string.Equals(value, "+", StringComparison.OrdinalIgnoreCase);

                    keyField.SetValue(null, equalsTrue);
                }
            }

            var methods = typeof(Program)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(method => method.Name)
                .ToList();

            var command = args.Length == 0 ? "help" : args[0 + leaveOut].ToLower();
            if (command != "help")
            {
                new Thread(Program.ConsoleKeyPressedProcLoop) { IsBackground = true }.Start();

                Console.WriteLine($"ZeroMQ v{zmq.LibraryVersion}");

                var method = methods.FirstOrDefault(m => m.Name.Equals(command, StringComparison.OrdinalIgnoreCase));
                if (method != null)
                {
                    object[]? parameters = null;
                    var arguments = method.GetParameters();

                    parameters = arguments.Length switch
                    {
                        2 => new object[] { dict, args.Skip(1 + leaveOut).ToArray() /* string[] args */ },
                        1 => new object[] { args.Skip(1 + leaveOut).ToArray() /* string[] args */ },
                        _ => parameters
                    };

                    var result = DebugStackTrace<TargetInvocationException>
                        .Invoke(method, /* static */null, parameters);

                    if (method.ReturnType == typeof(bool))
                        return (bool)result ? 0 : 1;
                    if (method.ReturnType == typeof(int))
                        return (int)result;

                    return 0; // method.ReturnType == typeof(Void)
                }

                returnMain = 1; // C bad
                Console.WriteLine();
                Console.WriteLine("Command invalid.");
            }

            Console.WriteLine();
            Console.WriteLine("Usage: ./" + AppDomain.CurrentDomain.FriendlyName + " [--option] <command>");

            if (fields.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Available [option]s:");
                foreach (var field in fields)
                    Console.WriteLine("  --{0}", field.Name);
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("Available <command>s:");

            foreach (var method in methods)
            {
                if (method.Name == "Main")
                    continue;
                if (0 < method.GetCustomAttributes(typeof(CompilerGeneratedAttribute), true).Length)
                    continue;

                Console.WriteLine("    {0}", method.Name);
            }

            Console.WriteLine();
            return returnMain;
        }
    }
}
