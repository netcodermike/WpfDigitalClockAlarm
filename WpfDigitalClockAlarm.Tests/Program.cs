using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace WpfDigitalClockAlarm.Tests
{
    public class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Console.WriteLine("======================================================");
            Console.WriteLine("   WpfDigitalClockAlarm Unit Test Suite");
            Console.WriteLine("======================================================");

            System.Threading.SynchronizationContext.SetSynchronizationContext(
                new System.Windows.Threading.DispatcherSynchronizationContext());
            int passed = 0;
            int failed = 0;
            var stopwatch = Stopwatch.StartNew();

            var methods = typeof(UnitTests).GetMethods(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in methods)
            {
                string testName = method.Name;
                try
                {
                    var result = method.Invoke(null, null);
                    if (result is Task asyncTask)
                    {
                        if (!asyncTask.IsCompleted)
                        {
                            var frame = new System.Windows.Threading.DispatcherFrame();
                            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                            _ = asyncTask.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
                            System.Windows.Threading.Dispatcher.PushFrame(frame);
                        }
                        asyncTask.GetAwaiter().GetResult();
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("[PASS] ");
                    Console.ResetColor();
                    Console.WriteLine(testName);
                    passed++;
                }
                catch (TargetInvocationException tie)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"{testName} - {tie.InnerException?.Message ?? tie.Message}");
                    failed++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"{testName} - {ex.Message}");
                    failed++;
                }
            }

            stopwatch.Stop();
            Console.WriteLine("------------------------------------------------------");
            Console.ForegroundColor = failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"Results: {passed} Passed, {failed} Failed out of {passed + failed} Tests ({stopwatch.ElapsedMilliseconds} ms)");
            Console.ResetColor();
            Console.WriteLine("======================================================");

            return failed == 0 ? 0 : 1;
        }
    }
}
