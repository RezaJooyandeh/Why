using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft
{
	public class Why
	{
		private Action<ISearchEvent> OnEvent { get; }
		private readonly Dictionary<string, JObject> _groupCache = new Dictionary<string, JObject>();
		private readonly Dictionary<string, JObject> _userCache = new Dictionary<string, JObject>();
		private readonly Dictionary<JObject, List<JObject>> _membersCache = new Dictionary<JObject, List<JObject>>();

		public Why(Action<ISearchEvent> events) => OnEvent = events;

		public async Task<IReadOnlyList<string>> Find(string ToCC, string token)
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

				var myEmail = await FindMyEmail(client);
				if (myEmail == null)
					return null;

				var emails = FindAllEmails(ToCC);
				if (emails == null)
					return null;

				var path = new List<string>();
				if (myEmail != null)
				{
					foreach (var email in emails)
					{
						var results = await FindMatches(client, email, myEmail);
						if (results != null)
							path.AddRange(results);
					}
				}

				return path;
			}
		}

		private List<string> FindAllEmails(string ToCC)
		{
			try
			{
				return ToCC
					.Split(';')
					.Select(ExtractEmail)
					.ToList();
			}
			catch (Exception e)
			{
				OnEvent(new ExceptionEvent("Unable to parse To and CC emails.", e));
				return null;
			}

			static string ExtractEmail(string x)
			{
				var start = x.LastIndexOf('<') + 1;
				if (start == 0)
					return x.Trim();
				var end = x.IndexOf('>', start + 1);

				return x.Substring(start, end - start);
			}
		}

		private async Task<string> FindMyEmail(HttpClient client)
		{
			var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me/");
			if (response.StatusCode == HttpStatusCode.OK)
			{
				using (var stream = new StreamReader(await response.Content.ReadAsStreamAsync()))
				{
					var json = (JObject)JsonConvert.DeserializeObject(stream.ReadToEnd());
					return json["mail"].ToString();
				}
			}
			else
				OnEvent(new ErrorEvent("Unable to find your email", response.StatusCode));

			return null;
		}

		private async Task<List<string>> FindMatches(HttpClient client, string email, string targetEmail)
		{
			var rootGroup = await FindGroup(email, ImmutableArray<string>.Empty);
			if (rootGroup == null)
			{
				var user = await FindUser(email);
				if (user == null)
				{
					OnEvent(new MemberNotFoundEvent(email, 1));
					return null;
				}

				var userDisplayName = user["displayName"].ToString();
				OnEvent(new MemberFoundEvent(email, userDisplayName, 1));
				if (email == targetEmail)
				{
					OnEvent(new MatchFoundEvent());

					return new List<string> { userDisplayName };
				}

				return null;
			}

			var list = await FindInGroup(rootGroup, 1, ImmutableArray<string>.Empty);
			return list?.Select(x => $@"{rootGroup["displayName"]}\{x}").ToList();

			async Task<JObject> FindGroup(string groupEmail, IImmutableList<string> parents)
			{
				if (parents.Contains(groupEmail))
				{
					OnEvent(new CycleDetectdEvent(groupEmail));
					return null;
				}

				if (_groupCache.TryGetValue(groupEmail, out var group))
					return group;

				var response = await client.GetAsync($"https://graph.microsoft.com/v1.0/groups?$filter=startsWith(mail, '{groupEmail}')");
				if (response.StatusCode != HttpStatusCode.OK)
				{
					OnEvent(new ErrorEvent($"Unable to information for {groupEmail}", response.StatusCode));
					return null;
				}

				using (var stream = new StreamReader(await response.Content.ReadAsStreamAsync()))
				{
					var json = (JObject)JsonConvert.DeserializeObject(stream.ReadToEnd());
					var results = (JArray)json["value"];
					group = results.Cast<JObject>().SingleOrDefault(x => (x["mail"]).Value<string>() == groupEmail);
					if (group != null)
						_groupCache[groupEmail] = group;
				}

				return group;
			}

			async Task<JObject> FindUser(string userEmail)
			{
				if (_userCache.TryGetValue(userEmail, out var user))
					return user;

				var response = await client.GetAsync($"https://graph.microsoft.com/v1.0/users?$filter=startsWith(mail, '{userEmail}')");
				if (response.StatusCode != HttpStatusCode.OK)
				{
					OnEvent(new ErrorEvent($"Unable to information for {userEmail}", response.StatusCode));
					return null;
				}

				using (var stream = new StreamReader(await response.Content.ReadAsStreamAsync()))
				{
					var json = (JObject)JsonConvert.DeserializeObject(stream.ReadToEnd());
					var results = (JArray)json["value"];
					user = results.Cast<JObject>().SingleOrDefault(x => (x["mail"]).Value<string>() == userEmail);
					if (user != null)
						_userCache[userEmail] = user;
				}

				return user;
			}

			async Task<List<JObject>> FindMembers(JObject group)
			{
				if (_membersCache.TryGetValue(group, out var members))
				{
					OnEvent(new GroupLoadedEvent(members.Count));
					return members;
				}

				var response = await client.GetAsync($"https://graph.microsoft.com/v1.0/groups/{group["id"]}/members");
				if (response.StatusCode != HttpStatusCode.OK)
				{
					OnEvent(new ErrorEvent($"Unable to find members of {group["displayName"]}", response.StatusCode));
					return null;
				}

				members = new List<JObject>();
				using (var stream = new StreamReader(await response.Content.ReadAsStreamAsync()))
				{
					var json = (JObject)JsonConvert.DeserializeObject(stream.ReadToEnd());
					members.AddRange(((JArray)json["value"]).Cast<JObject>().ToList());

					while (json.Children().Where(x => x is JProperty).Cast<JProperty>().Any(x => x.Name == "@odata.nextLink"))
					{
						var nextResponse = await client.GetAsync(json["@odata.nextLink"].ToString());
						if (nextResponse.StatusCode != HttpStatusCode.OK)
							OnEvent(new ErrorEvent($"Unable to continue loading members of {group["displayName"]}", response.StatusCode));
						else
						{
							using (var nextStream = new StreamReader(await nextResponse.Content.ReadAsStreamAsync()))
							{
								json = (JObject)JsonConvert.DeserializeObject(nextStream.ReadToEnd());
								members.AddRange(((JArray)json["value"]).Cast<JObject>().ToList());
							}
						}
					}

					_membersCache[group] = members;
				}

				OnEvent(new GroupLoadedEvent(members.Count));
				return members;
			}

			async Task<List<string>> FindInGroup(JObject group, int level, IImmutableList<string> parents)
			{
				if (group == null)
					return null;

				var groupEmail = group["mail"].ToString();
				OnEvent(new MemberFoundEvent(groupEmail, group["displayName"].ToString(), level));
				OnEvent(new GroupLoadingEvent());

				var members = await FindMembers(group);
				if (members == null)
					return null;

				var childrensParent = parents.Add(groupEmail);

				IEnumerable<string> result = new string[] { };
				foreach (var child in members)
				{
					var childEmail = child["mail"].ToString();
					if (childEmail == targetEmail)
					{
						result = result.Concat(new List<string> { child["displayName"].ToString() });
						OnEvent(new MatchFoundEvent());
					}

					if (child["@odata.type"].ToString() != "#microsoft.graph.group")
						continue;

					if (childEmail == groupEmail)
						continue;

					if (childEmail == "")
						continue;

					var subSearch = await FindInGroup(await FindGroup(childEmail, childrensParent), level + 1, childrensParent);
					if (subSearch != null)
						result = result.Concat(subSearch.Select(x => $@"{child["displayName"]}\{x}"));
				}

				return result.Any() ? result.ToList() : null;
			}
		}
	}
}
