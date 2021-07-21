using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using ModbusHelper.Models;
namespace ClientSample
{
    class Program
    {
        static ModbusHelper.Modbus modbus = new ModbusHelper.Modbus();
        static List<AtomModel> lst;
         
        static void Main(string[] args)
        {
            lst = new List<AtomModel>()
            {
                new AtomModel(){ Name="test1", RegAddress=40001, BitOrder= BITORDER.A, setValueEventhandler=setValueCallback},
                new AtomModel(){Name="222",RegAddress=40002, BitOrder= BITORDER.ABCD,setValueEventhandler=setValueCallback},
                new AtomModel(){ Name="333", RegAddress=40004, BitOrder= BITORDER.ABCD,setValueEventhandler=setValueCallback},
                new AtomModel(){Name="444",RegAddress=40006, BitOrder= BITORDER.ABCD,setValueEventhandler=setValueCallback},
                new AtomModel(){Name="double test",RegAddress=40008, BitOrder= BITORDER.ABCDEFGH,setValueEventhandler=setValueCallback},
                new AtomModel(){Name="end",RegAddress=49999, BitOrder= BITORDER.A},
                new AtomModel(){Name="3331",RegAddress=30001, BitOrder= BITORDER.A },
                new AtomModel(){Name="3332",RegAddress=30002, BitOrder= BITORDER.A },
                new AtomModel(){Name="3333",RegAddress=30003, BitOrder= BITORDER.A },
                new AtomModel(){Name="3334",RegAddress=30004, BitOrder= BITORDER.A },
                new AtomModel(){Name="3335",RegAddress=30005, BitOrder= BITORDER.A },
            };

            modbus.InitModbusReg(lst);

            TcpListener listener = new TcpListener(12345);
            listener.Start();

            listener.BeginAcceptSocket(ListenerBeginCall, listener);
           
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        static void ListenerBeginCall(IAsyncResult iResult)
        {
            var listener = iResult.AsyncState as TcpListener;
            Socket clientSocket = listener.EndAcceptSocket(iResult);
            ConnectModel connect = new ConnectModel() { rData = new byte[1024], client = clientSocket };
            clientSocket.BeginReceive(connect.rData, 0, 1024, 0, ClientCallBace, connect);
            listener.BeginAcceptSocket(ListenerBeginCall, listener);
        }
        static void ClientCallBace(IAsyncResult asyncCall)
        {
            ConnectModel connect = asyncCall.AsyncState as ConnectModel;
            var len = connect.client.EndReceive(asyncCall);
            if (len == 0)
            {
                connect.client.Shutdown(SocketShutdown.Both);
                connect.client.Dispose();
                return;
            }
            byte[] recieveData = new byte[len];
            Array.Copy(connect.rData, recieveData, len);
            var backArray=modbus.ProcessRecieveCmd(recieveData);
            if (backArray != null)
                connect.client.Send(backArray);
            connect.client.BeginReceive(connect.rData, 0, 1024, 0, ClientCallBace, connect);
        }
        static async Task<ModbusStatus> setValueCallback(AtomModel atom)
        {
            if (atom.RegAddress == 40006)
            {
                atom.BackToLastData();
                return ModbusStatus.ERR;
            }
            Console.WriteLine(atom.RegAddress +":"+ atom.Name + "has changed");
            return ModbusStatus.OK;
        }
    }
    public class ConnectModel
    {
        public Socket client { get; set; }
        public byte[] rData { get; set; }
    }
}
