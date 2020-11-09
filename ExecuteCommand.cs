using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EAL.Interfaces;
using EAL.Structures;

namespace _6112020_SunnenSafetyParameterEALTest
{
    class ExecuteCommand
    {
        private static IEALConnection _ealConnection;
        private static string s;
        private static string[] validParmeters = null;

        private static bool cmdExecutedCorrectly = false;
        private static bool tempReturn;

        public ExecuteCommand(IEALConnection aEalConnection)
        {
            _ealConnection = aEalConnection;

        }

        public static bool RunCommand(string cmdParameter)
        {
            if (null == validParmeters)
            {
                try
                {
                    s = _ealConnection.Parameter.ReadDataAsString("S-0-0025.0.0");
                    validParmeters = s.Split(' ');
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to get parameters");
                    return false;
                }
            }

            if (validParmeters.Contains(cmdParameter) == true)
            {
                int parameterStatus = 0;
                tempReturn = false;
                var cts = new CancellationTokenSource();
                var token = cts.Token;
                object padlock = new object();

                //Start Thread 2
                var t2 = Task.Factory.StartNew(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        //bool lockTaken = false;
                        try
                        {
                           // sl.Enter(ref lockTaken);

                            Console.WriteLine("####################################" + " " + $"Command has started on thread {Thread.CurrentThread.ManagedThreadId} " + "####################################" + "\n");

                            _ealConnection.Parameter.Command(cmdParameter);
                        }
                        catch (Exception ex)
                        {
                            tempReturn = cmdExecutedCorrectly = false; parameterStatus = 0;
                            MessageBox.Show(String.Format("Error while executing command \nMessage:{0}", ex.Message));
                        }

                    }, token);

                //Start Thread 1
                var t = Task.Factory.StartNew(()  =>
                {
                    for (int i = 0; i < 500; i += 1)
                    {
                        token.ThrowIfCancellationRequested();

                        try
                        {
                            //lock (padlock)
                            //{
                                parameterStatus = _ealConnection.Parameter.ReadDataDirectStatus(cmdParameter); //Read parameter status (0= Not set and not enabled, 3= Command correctly executed, 5= Command execution interrupted, 7= In process, 15= Error, command execution impossible)
                                Console.WriteLine($"Count:{i} on thread {Thread.CurrentThread.ManagedThreadId}" + $" - {cmdParameter} - Value = " + _ealConnection.Parameter.ReadData(cmdParameter) + $", Status = " + parameterStatus);

                                if (3 == parameterStatus) //Checking to make sure commmand was correctly executed
                                {
                                    cmdExecutedCorrectly = true;
                                }
                                if (0 == parameterStatus && cmdExecutedCorrectly) //Looks to see if the command finished executing
                                {
                                    cmdExecutedCorrectly = false; parameterStatus = 0; tempReturn = true;
                                    break;
                                }
                                if (15 == parameterStatus) //Error, command execution impossible 
                                {
                                    tempReturn = cmdExecutedCorrectly = false; parameterStatus = 0;
                                    break;
                                }
                           // }                                                   
                        }
                        catch (Exception ex)
                        {
                            tempReturn = cmdExecutedCorrectly = false; parameterStatus = 0;
                            MessageBox.Show(String.Format("Error while executing command \nMessage:{0}", ex.Message));
                        }
                    }
                }, token);

                try
                {                  
                    t.Wait(); t2.Wait();      
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e);
                }

                return tempReturn;
            }
            else
            {
                MessageBox.Show("The parameter you selected is not a valid command parameter");
                return false;
            }

        }

       
    }
}
