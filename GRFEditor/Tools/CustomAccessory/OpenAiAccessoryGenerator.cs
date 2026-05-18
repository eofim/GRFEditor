using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GRFEditor.ApplicationConfiguration;

namespace GRFEditor.Tools.CustomAccessory {
	public static class OpenAiAccessoryGenerator {
		public static bool TrySuggest(CustomAccessoryEntry entry, out string error) {
			error = null;

			if (!GrfEditorConfiguration.CustomAccessoryUseOpenAi) {
				error = "OpenAI não está habilitada nas configurações.";
				return false;
			}

			var apiKey = GrfEditorConfiguration.CustomAccessoryOpenAiApiKey;
			if (string.IsNullOrWhiteSpace(apiKey)) {
				error = "Informe a chave da API OpenAI nas configurações.";
				return false;
			}

			try {
				var prompt = BuildPrompt(entry);
				var response = CallChatCompletion(apiKey.Trim(), GrfEditorConfiguration.CustomAccessoryOpenAiModel, prompt);
				return TryParseResponse(response, entry, out error);
			}
			catch (WebException ex) {
				error = ReadWebError(ex) ?? ex.Message;
				return false;
			}
			catch (Exception ex) {
				error = ex.Message;
				return false;
			}
		}

		private static string BuildPrompt(CustomAccessoryEntry entry) {
			return "Você ajuda a cadastrar visuais de acessório/costume no Ragnarok Online.\n" +
			       "Com base no sprite \"" + entry.SpritePath + "\" responda APENAS um JSON compacto no formato:\n" +
			       "{\"constantName\":\"ACCESSORY_exemplo\",\"viewId\":1234,\"displayName\":\"_exemplo\"}\n" +
			       "Regras: constantName deve começar com ACCESSORY_; displayName deve começar com _ e refletir o nome do arquivo .spr.\n" +
			       "Se já existir convenção no nome do arquivo, preserve-a. viewId deve ser um inteiro positivo sugerido (pode ser " + entry.ViewId + ").";
		}

		private static string CallChatCompletion(string apiKey, string model, string prompt) {
			if (string.IsNullOrWhiteSpace(model))
				model = "gpt-4o-mini";

			var body = "{\"model\":\"" + EscapeJson(model) + "\",\"temperature\":0.2,\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}]}";
			var request = (HttpWebRequest)WebRequest.Create("https://api.openai.com/v1/chat/completions");
			request.Method = "POST";
			request.ContentType = "application/json";
			request.Headers.Add("Authorization", "Bearer " + apiKey);
			request.Timeout = 120000;

			var payload = Encoding.UTF8.GetBytes(body);
			request.ContentLength = payload.Length;

			using (var stream = request.GetRequestStream()) {
				stream.Write(payload, 0, payload.Length);
			}

			using (var response = (HttpWebResponse)request.GetResponse())
			using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) {
				return reader.ReadToEnd();
			}
		}

		private static bool TryParseResponse(string rawResponse, CustomAccessoryEntry entry, out string error) {
			error = null;
			if (string.IsNullOrWhiteSpace(rawResponse)) {
				error = "Resposta vazia da OpenAI.";
				return false;
			}

			var contentMatch = Regex.Match(rawResponse, @"""content""\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.Singleline);
			if (!contentMatch.Success) {
				error = "Não foi possível interpretar a resposta da API.";
				return false;
			}

			var content = UnescapeJson(contentMatch.Groups[1].Value);
			var jsonMatch = Regex.Match(content, @"\{[^{}]*\}");
			if (!jsonMatch.Success) {
				error = "JSON não encontrado na resposta.";
				return false;
			}

			var json = jsonMatch.Value;
			var constantMatch = Regex.Match(json, @"""constantName""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
			var displayMatch = Regex.Match(json, @"""displayName""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
			var viewIdMatch = Regex.Match(json, @"""viewId""\s*:\s*(\d+)", RegexOptions.IgnoreCase);

			if (!constantMatch.Success || !displayMatch.Success) {
				error = "Campos constantName/displayName ausentes.";
				return false;
			}

			entry.ConstantName = constantMatch.Groups[1].Value.Trim();
			entry.DisplayName = displayMatch.Groups[1].Value.Trim();

			if (viewIdMatch.Success)
				entry.ViewId = int.Parse(viewIdMatch.Groups[1].Value);

			if (!entry.ConstantName.StartsWith("ACCESSORY_", StringComparison.OrdinalIgnoreCase))
				entry.ConstantName = "ACCESSORY_" + entry.ConstantName.TrimStart('_');

			if (!entry.DisplayName.StartsWith("_", StringComparison.Ordinal))
				entry.DisplayName = "_" + entry.DisplayName.TrimStart('_');

			return true;
		}

		private static string EscapeJson(string value) {
			if (value == null)
				return "";

			return value
				.Replace("\\", "\\\\")
				.Replace("\"", "\\\"")
				.Replace("\r", "\\r")
				.Replace("\n", "\\n")
				.Replace("\t", "\\t");
		}

		private static string UnescapeJson(string value) {
			return value
				.Replace("\\n", "\n")
				.Replace("\\r", "\r")
				.Replace("\\t", "\t")
				.Replace("\\\"", "\"")
				.Replace("\\\\", "\\");
		}

		private static string ReadWebError(WebException ex) {
			try {
				if (ex.Response == null)
					return null;

				using (var stream = ex.Response.GetResponseStream())
				using (var reader = new StreamReader(stream ?? Stream.Null)) {
					return reader.ReadToEnd();
				}
			}
			catch {
				return null;
			}
		}
	}
}
