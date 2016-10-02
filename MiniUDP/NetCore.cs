﻿/*
 *  MiniUDP - A Simple UDP Layer for Shipping and Receiving Byte Arrays
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MiniUDP
{
  public delegate void NetPeerConnectEvent(NetPeer peer, string token);

  public class NetCore
  {
    public event NetPeerConnectEvent PeerConnected;

    private readonly NetController controller;
    private readonly NetSocket socket;
    private readonly INetSocketWriter writer;
    private readonly byte[] reusableBuffer;
    private Thread controllerThread;

    public NetCore(string version, bool allowConnections)
    {
      if (version == null)
        version = "";

      this.socket = new NetSocket();
      this.writer = this.socket.CreateWriter();
      this.reusableBuffer = new byte[NetConfig.SOCKET_BUFFER_SIZE];
      this.controller = 
        new NetController(
          this.socket.CreateReader(), // Not thread safe, need one per thread
          this.socket.CreateWriter(), // Not thread safe, need one per thread
          version,
          allowConnections);
    }

    public NetPeer Connect(IPEndPoint endpoint, string token)
    {
      NetPeer peer = this.AddConnection(endpoint, token);
      this.Start();
      return peer;
    }

    public void Host(int port)
    {
      this.socket.Bind(port);
      this.Start();
    }

    private void Start()
    {
      this.controllerThread = 
        new Thread(new ThreadStart(this.controller.Start));
      this.controllerThread.IsBackground = true;
      this.controllerThread.Start();
    }

    public NetPeer AddConnection(IPEndPoint endpoint, string token)
    {
      if (token == null)
        token = "";
      NetPeer pending = this.controller.BeginConnect(endpoint, token);
      pending.Expose(this);
      return pending;
    }

    public void Stop(int timeout = 1000)
    {
      this.controller.Stop();
      this.controllerThread.Join(timeout);
      this.socket.Close();
    }

    public void PollEvents()
    {
      NetEvent evnt;
      while (this.controller.TryReceiveEvent(out evnt))
      {
        NetPeer peer = evnt.Peer;

        // No events should fire if the user closed the peer
        if (peer.ClosedByUser == false)
        {
          switch (evnt.EventType)
          {
            case NetEventType.PeerConnected:
              peer.Expose(this);
              this.PeerConnected?.Invoke(peer, peer.Token);
              break;

            case NetEventType.PeerClosedError:
              peer.OnPeerClosedError((SocketError)evnt.OtherData);
              break;

            case NetEventType.PeerClosedTimeout:
              peer.OnPeerClosedTimeout();
              break;

            case NetEventType.PeerClosedShutdown:
              peer.OnPeerClosedShutdown();
              break;

            case NetEventType.PeerClosedKicked:
              byte userReason;
              NetKickReason reason =
                NetEvent.ReadReason(evnt.OtherData, out userReason);
              peer.OnPeerClosedKicked(reason, userReason);
              break;

            case NetEventType.ConnectTimedOut:
              peer.OnConnectTimedOut();
              break;

            case NetEventType.ConnectAccepted:
              peer.OnConnectAccepted();
              break;

            case NetEventType.ConnectRejected:
              peer.OnConnectRejected((NetRejectReason)evnt.OtherData);
              break;

            case NetEventType.Payload:
              peer.OnPayloadReceived(evnt.EncodedData, evnt.EncodedLength);
              break;

            default:
              throw new NotImplementedException();
          }
        }

        this.controller.RecycleEvent(evnt);
      }
    }

    internal void OnPeerClosed(NetPeer peer, byte reason)
    {
      this.SendUserDisconnect(peer.EndPoint, reason);
    }

    internal SocketError SendPayload(
      NetPeer peer, 
      ushort sequence, 
      byte[] data, 
      int length)
    {
      int position = NetIO.PackPayloadHeader(this.reusableBuffer, sequence);
      Array.Copy(data, 0, this.reusableBuffer, position, length);
      position += length;
      return this.writer.TrySend(peer.EndPoint, this.reusableBuffer, position);
    }

    private void SendUserDisconnect(
      IPEndPoint source,
      byte userReason)
    {
      NetDebug.LogMessage("Sending disconnect " + userReason);
      int length =
        NetIO.PackProtocolHeader(
          this.reusableBuffer,
          NetPacketType.Disconnect,
          (byte)NetKickReason.User,
          userReason);
      this.writer.TrySend(source, this.reusableBuffer, length);
    }
  }
}