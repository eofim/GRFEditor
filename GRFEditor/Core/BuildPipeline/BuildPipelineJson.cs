using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GRFEditor.Core.BuildPipeline {
	internal static class BuildPipelineJson {
		public static string Serialize<T>(T value) {
			if (value == null)
				return "null";

			var serializer = new DataContractJsonSerializer(typeof(T));
			using (var stream = new MemoryStream()) {
				serializer.WriteObject(stream, value);
				return Encoding.UTF8.GetString(stream.ToArray());
			}
		}

		public static T Read<T>(string json) where T : class {
			if (String.IsNullOrWhiteSpace(json))
				return null;

			var serializer = new DataContractJsonSerializer(typeof(T));
			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) {
				return serializer.ReadObject(stream) as T;
			}
		}
	}
}
