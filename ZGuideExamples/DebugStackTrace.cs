using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Examples
{
    internal static class DebugStackTrace<TException>
        where TException : Exception
    {
        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        public static object? Invoke(MethodInfo method, object target, params object[] args)
        {
            $"Invoking \"{method.Name}\"".DumpString();
            try
            {
                return method.Invoke(target, args);
            }
            catch (TException te)
            {
                if (te.InnerException == null)
                    throw;

                ExceptionDispatchInfo.Capture(te.InnerException).Throw();

                throw;
            }
            catch (Exception ex)
            {

                Console.Error.WriteLine(nameof(DebugStackTrace<TException>));
                Console.Error.WriteLine(ex.GetType().AssemblyQualifiedName);
                Console.Error.WriteLine(ex.ToString());

                var i = 0;
                var iex = ex;
                while ((iex = iex.InnerException) is not null)
                {
                    Console.Error.WriteLine($"== INNER EXCEPTION {i} ==");
                    Console.Error.WriteLine(ex.GetType().AssemblyQualifiedName);
                    Console.Error.WriteLine(ex.ToString());
                }

                // wtf
                Debugger.Launch();
                Debugger.Break();
                throw;
            }
        }
    }
}
