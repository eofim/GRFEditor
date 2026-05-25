using System;
using System.Security.Cryptography;
using System.Text;
using Utilities.Hash;

namespace GRFEditor.Core.BuildPipeline {
	internal sealed class BuildPipelineSha1Hash : IHash {
		public BuildPipelineSha1Hash() {
			Error = new byte[20];
			for (int i = 0; i < Error.Length; i++)
				Error[i] = 255;
		}

		public byte[] Error { get; private set; }

		public int HashLength => 20;

		public string ComputeHash(byte[] data) {
			using (SHA1 sha1 = SHA1.Create()) {
				byte[] hash = sha1.ComputeHash(data);
				var sb = new StringBuilder(hash.Length * 2);
				foreach (byte b in hash)
					sb.AppendFormat("{0:x2}", b);
				return sb.ToString();
			}
		}

		public byte[] ComputeByteHash(byte[] data) {
			using (SHA1 sha1 = SHA1.Create())
				return sha1.ComputeHash(data);
		}
	}
}
