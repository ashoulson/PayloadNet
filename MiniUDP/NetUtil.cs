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
using System.Security.Cryptography;

namespace MiniUDP
{
  internal static class NetUtil
  {
    /// <summary>
    /// Compares two bytes with wrap-around arithmetic
    /// </summary>
    internal static int ByteSeqDiff(byte a, byte b)
    {
      // Assumes a sequence is 8 bits
      return (int)((a << 24) - (b << 24)) >> 24;
    }

    /// <summary>
    /// Compares two ushorts with wrap-around arithmetic
    /// </summary>
    internal static int UShortSeqDiff(ushort a, ushort b)
    {
        // Assumes a stamp is 16 bits
        return (int)((a << 16) - (b << 16)) >> 16;
    }

    /// <summary>
    /// Creates a 32-bit cryptographically random unique ID
    /// </summary>
    /// <returns></returns>
    internal static uint CreateUniqueID()
    {
      byte[] bytes = new byte[4];
      RandomNumberGenerator.Create().GetBytes(bytes);
      return BitConverter.ToUInt32(bytes, 0);
    }
  }
}
