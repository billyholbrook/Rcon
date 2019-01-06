using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RconConsoleTest
{
    public class Rcon
    {
        private class Packet
        {
            private PacketType type;
            private string data;
            private int id;
            private int size;

            public Packet(string command, PacketType packetType)
            {
                this.type = packetType;
                this.id = packetID;
                this.size = 14 + command.Length;
                this.data = command;
            }

            public Packet(byte[] packet)
            {
                int packetLength = packet.Length;
                this.size = packetLength;

                byte[] id = new byte[4];
                Buffer.BlockCopy(packet, 0, id, 0, 4);
                this.id = BitConverter.ToInt32(id, 0);

                byte[] type = new byte[4];
                Buffer.BlockCopy(packet, 4, type, 0, 4);
                this.type = (PacketType)BitConverter.ToInt32(type, 0);

                byte[] data = new byte[packetLength - 9];
                Buffer.BlockCopy(packet, 8, data, 0, packetLength - 9);
                this.data = Encoding.Default.GetString(data);
            }

            public byte[] ToByteArray()
            {
                byte[] packet = new byte[14 + this.data.Length];
                byte[] size = BitConverter.GetBytes(10 + this.data.Length);
                byte[] id = BitConverter.GetBytes(this.id);
                byte[] type = BitConverter.GetBytes((int)this.type);

                int i = 0;
                for (; i < 4; i++)
                {
                    packet[i] = size[i];
                }
                for (; i < 8; i++)
                {
                    packet[i] = id[i - 4];
                }
                for (; i < 12; i++)
                {
                    packet[i] = type[i - 8];
                }
                for (; i < 12 + this.data.Length; i++)
                {
                    packet[i] = (byte)this.data[i - 12];
                }
                packet[i] = 0;

                return packet;
            }

            public override string ToString()
            {
                return this.data;
            }

            public int Size()
            {
                return this.size;
            }

            public int GetID()
            {
                return this.id;
            }

            public PacketType GetPacketType()
            {
                return this.type;
            }
        }

        private class StateObject
        {
            public int packetSize;
            public int currentSize;
            public bool isFirstPacket;
            public byte[] buffer;
        }

        private IPAddress ip = null;
        private int port = 0;
        private Socket socket = null;
        private String response = String.Empty;

        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);
        private static int packetID = 0;
        private static bool isAuthenticated = false;

        private enum PacketType
        {
            SERVERDATA_AUTH = 3,
            SERVERDATA_AUTH_RESPONSE = 2,
            SERVERDATA_EXECCOMMAND = 2,
            SERVERDATA_RESPONSE_VALUE = 0
        }

        public Rcon(IPAddress ip, int port, string rconPassword)
        {
            this.ip = ip;
            this.port = port;
            InitializeSocket();
            Authenticate(rconPassword);
        }

        private void InitializeSocket()
        {
            IPEndPoint endPoint = new IPEndPoint(ip, port);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.BeginConnect(endPoint, new AsyncCallback(ConnectCallback), socket);
            connectDone.WaitOne();
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            socket.EndConnect(ar);

            // Connection is complete 
            connectDone.Set();
        }

        private void Send(Packet packet)
        {
            socket.BeginSend(packet.ToByteArray(), 0, packet.Size(), 0,
            new AsyncCallback(SendCallback), socket);
            packetID++;
        }

        private void SendCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndSend(ar);

            // Sending is complete
            sendDone.Set();
        }

        private void Receive()
        {
            StateObject state = new StateObject();

            state.buffer = new byte[4];
            state.isFirstPacket = true;

            // Get the size of the packet 
            socket.BeginReceive(state.buffer, 0, 4, 0,
                new AsyncCallback(ReceiveCallback), state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;

            // If the first chunk of packet, use the size to receive the rest of the packet
            if (state.isFirstPacket)
            {
                state.isFirstPacket = false;
                socket.EndReceive(ar);
                state.packetSize = BitConverter.ToInt32(state.buffer, 0);
                state.currentSize = 0;
                state.buffer = new byte[state.packetSize];
                socket.BeginReceive(state.buffer, 0, state.packetSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            else
            {
                // Read the data
                int bytesRead = socket.EndReceive(ar);
                state.currentSize += bytesRead;

                if (state.packetSize != state.currentSize)
                {
                    // Get the rest of the packet.  
                    socket.BeginReceive(state.buffer, state.currentSize, state.packetSize - state.currentSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // All of the packet has arrived, process it
                    Packet packet = new Packet(state.buffer);
                    ProcessPacket(packet);
                }
            }
        }

        private void ProcessPacket(Packet packet)
        {
            switch (packet.GetPacketType())
            {
                case PacketType.SERVERDATA_RESPONSE_VALUE:
                    if (isAuthenticated)
                    {
                        //Receiving is complete
                        response = packet.ToString();
                        receiveDone.Set();
                    }
                    else
                    {
                        // Since we aren't authenticated and we didn't receive a SERVERDATA_AUTH_RESPONSE packet, it will be the next packet
                        Receive();
                    }
                    break;
                case PacketType.SERVERDATA_AUTH_RESPONSE:
                    if (packet.GetID() != -1)
                    {
                        isAuthenticated = true;
                    }
                    //Receiving is complete
                    receiveDone.Set();
                    break;
            }
        }

        private bool Authenticate(string rconPassword)
        {
            Packet packet = new Packet(rconPassword, PacketType.SERVERDATA_AUTH);

            Send(packet);
            sendDone.WaitOne();
            sendDone.Reset();

            Receive();
            receiveDone.WaitOne();
            receiveDone.Reset();

            return isAuthenticated;
        }

        public string SendCommand(string command)
        {
            Packet packet = new Packet(command, PacketType.SERVERDATA_EXECCOMMAND);

            Send(packet);
            sendDone.WaitOne();
            sendDone.Reset();

            Receive();
            receiveDone.WaitOne();
            receiveDone.Reset();

            return response;
        }
    }
}
