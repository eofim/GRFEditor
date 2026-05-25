using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GRFEditor.Core.ProjectProfiles {
	internal static class ProjectProfileJsonSerializer {
		public static string Serialize(ProjectProfile profile) {
			if (profile == null)
				throw new ArgumentNullException(nameof(profile));

			profile.EnsureDefaults();

			using (var stream = new MemoryStream()) {
				var serializer = new DataContractJsonSerializer(typeof(ProjectProfile));
				serializer.WriteObject(stream, profile);
				return Encoding.UTF8.GetString(stream.ToArray());
			}
		}

		public static ProjectProfile Deserialize(string json) {
			if (String.IsNullOrWhiteSpace(json))
				return null;

			byte[] bytes = Encoding.UTF8.GetBytes(json);

			using (var stream = new MemoryStream(bytes)) {
				var serializer = new DataContractJsonSerializer(typeof(ProjectProfile));
				var profile = serializer.ReadObject(stream) as ProjectProfile;

				if (profile != null)
					profile.EnsureDefaults();

				return profile;
			}
		}

		public static void WriteToFile(string filePath, ProjectProfile profile) {
			string json = Serialize(profile);
			File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}

		public static ProjectProfile ReadFromFile(string filePath) {
			if (!File.Exists(filePath))
				return null;

			return Deserialize(File.ReadAllText(filePath, Encoding.UTF8));
		}
	}
}
