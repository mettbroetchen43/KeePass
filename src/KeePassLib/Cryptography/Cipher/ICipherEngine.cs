/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.IO;

namespace KeePassLib.Cryptography.Cipher
{
	public interface ICipherEngine
	{
		/// <summary>
		/// UUID of the engine. If you want to write an engine/plugin,
		/// please contact the KeePass team to obtain a new UUID.
		/// </summary>
		PwUuid CipherUuid
		{
			get;
		}

		/// <summary>
		/// Name displayed in the list of available encryption/decryption
		/// engines in the GUI.
		/// </summary>
		string DisplayName
		{
			get;
		}

		Stream EncryptStream(Stream s, byte[] pbKey, byte[] pbIV);
		Stream DecryptStream(Stream s, byte[] pbKey, byte[] pbIV);
	}

	public interface ICipherEngine2 : ICipherEngine
	{
		/// <summary>
		/// Length of an encryption key in bytes.
		/// The base <c>ICipherEngine</c> assumes 32.
		/// </summary>
		int KeyLength
		{
			get;
		}

		/// <summary>
		/// Length of the initialization vector in bytes.
		/// The base <c>ICipherEngine</c> assumes 16.
		/// </summary>
		int IVLength
		{
			get;
		}
	}
}
