using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace udp_over_tcp
{
    class Program
    {
        enum scenario
        {
            None,
            inverse_ack,
            timeout,
            corrupt_data
        };

        static scenario global_scenario=new scenario();
        static Timer aTimer = new Timer();

        static void Main(string[] args)
        {
              aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
              aTimer.Interval = 1000;
              aTimer.Enabled = true;
            
              UdpClient udpServer = new UdpClient(11000);
            
              Parallel.Invoke(
                               () => server(udpServer),
                               () => client()
                               );
            
        }
        
        static string Checksum(string data)
        {
            //byte[] byteToCalculate = new byte[2] { 0xE6, 0x66 };

            byte[] byteToCalculate = Encoding.ASCII.GetBytes(data);
            int checksum = 0;
            foreach (byte chData in byteToCalculate)
            {
                checksum += chData;
            }
            checksum &= 0xff;
            return checksum.ToString("X2");
        }

        private static void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            elapsed_intervals++;
            if (view_timeout)
            {
                Console.Write(".");
            }
        }
        static bool view_timeout=false;

        static void server(UdpClient udpServer)
        {
            while (true)
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 11000);
                var data = udpServer.Receive(ref remoteEP); // listen on port 11000
                string[] recieved_message = new string[3];
                recieved_message = Encoding.UTF8.GetString(data).Split(',');
                int seq_number = Convert.ToInt16(recieved_message[0].Substring(recieved_message[0].IndexOf(':') + 1));

                bool check_sum_result = false;

                string recieved_check_sum = recieved_message[1].Substring(recieved_message[1].IndexOf(':') + 1);
                string calculated_check_sum = Checksum(recieved_message[2]);

                if (recieved_check_sum== calculated_check_sum)
                {
                    check_sum_result = true;
                }
                if (global_scenario==scenario.inverse_ack)
                {
                    if (seq_number == 0) seq_number = 1;
                    else seq_number = 0;
                }
                if(global_scenario == scenario.timeout)
                {
                    view_timeout = true;
                    System.Threading.Thread.Sleep(5000);
                }
                if (global_scenario == scenario.corrupt_data)
                {
                    if (seq_number == 0) seq_number = 1;
                    else seq_number = 0;

                    check_sum_result = false;
                }
                view_timeout = false;
                
                Console.Write("Server receive message : \"" + recieved_message[2] +
                              "\" seq_num : " + seq_number.ToString() +
                              " check_sum : " + check_sum_result.ToString() +
                              " from  " + remoteEP.ToString() + Environment.NewLine);

                String reply = "ack:" + seq_number;
                int size = ASCIIEncoding.ASCII.GetByteCount(reply);
                byte[] message_array = Encoding.ASCII.GetBytes(reply);
                udpServer.Send(message_array, size, remoteEP);

            }
        }
        static int required_ack;
        static int elapsed_intervals=0;
        static int prev_interval;

        static void client()
        {
            while (true)
            {
                Console.WriteLine("write message:");
                String message = Console.ReadLine();
                Console.WriteLine("0=> no errors , 1=> inverse ack , 2=>timeout , 3=>corrupt Data");
                int condition =Convert.ToInt16(Console.ReadLine());
              
                switch (condition)
                {
                    case 0:
                        global_scenario = scenario.None;
                        break;
                    case 1:
                        global_scenario = scenario.inverse_ack;
                        break;
                    case 2:
                        global_scenario = scenario.timeout;
                        break;
                    case 3:
                        global_scenario = scenario.corrupt_data;
                        break;
                }

                String true_message = message;
                String check_sum_output = Checksum(true_message);
               var client = new UdpClient();
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000); // endpoint where server is listening
                client.Connect(ep);

                required_ack = 0;
                prev_interval = elapsed_intervals;
               
                message = "packet:0,check_sum:"+ check_sum_output+"," + message;

                int size = ASCIIEncoding.ASCII.GetByteCount(message);
                byte[] message_array = Encoding.ASCII.GetBytes(message);
                client.Send(message_array, size);

             //handeling both no errors & inverse_ack
             while (true)
             {
                var receivedData_0 = client.Receive(ref ep);
                String received_message_0 = Encoding.UTF8.GetString(receivedData_0);
                if (received_message_0 != null)
                {
                      Console.Write("client recieved: " + received_message_0 + Environment.NewLine);
                        
                        if(elapsed_intervals <= prev_interval + 1)
                        {
                            prev_interval = elapsed_intervals;

                            //if ack == 0 ==>send next packet
                            if (Convert.ToInt16(received_message_0.Substring(received_message_0.IndexOf(':') + 1)) == required_ack)
                            {
                                if (required_ack == 1 && Convert.ToInt16(received_message_0.Substring(received_message_0.IndexOf(':') + 1)) == 1)
                                { break; }

                                required_ack++;

                                String seq_1_message = "packet:" + required_ack + "," + "check_sum:" + check_sum_output + "," + true_message;
                                int size_1 = ASCIIEncoding.ASCII.GetByteCount(seq_1_message);
                                byte[] message_array_1 = Encoding.ASCII.GetBytes(seq_1_message);
                                client.Send(message_array_1, size_1);
                            }
                            else //ack=1 and we wanted it 0 ==> send again
                            {
                                String message_0_again = "packet:" + required_ack + ","+ "check_sum:" + check_sum_output + ","  + true_message;

                                int size_0_again = ASCIIEncoding.ASCII.GetByteCount(message_0_again);
                                byte[] message_array_0_again = Encoding.ASCII.GetBytes(message_0_again);
                                client.Send(message_array_0_again, size_0_again);
                                global_scenario = scenario.None;
                            }
                        }
                        else //timeout occurs  ==>send message again
                        {
                            prev_interval = elapsed_intervals;

                            String message_0_again = "packet:" + required_ack + "," + "check_sum:" + check_sum_output + "," + true_message;

                            int size_0_again = ASCIIEncoding.ASCII.GetByteCount(message_0_again);
                            byte[] message_array_0_again = Encoding.ASCII.GetBytes(message_0_again);
                            client.Send(message_array_0_again, size_0_again);
                            global_scenario = scenario.None;
                        }
                       
                    }
                }
            }
        }
    }
}
