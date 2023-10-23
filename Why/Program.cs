using CommandLine;
using System;
using System.Threading.Tasks;
using UsingConsoleColors;

namespace Microsoft
{
	internal class Program
	{
		private static async Task Main(string[] args)
		{
			Task result = null;
			Parser.Default.ParseArguments<Options>(args)
				.WithParsed(options => result = Find(options.ToCC, options.Token, options.LookFor));

			if (result != null)
				await result;
		}

		private static async Task Find(string toCC, string token, string lookFor)
		{
			using (new ConsoleForegroundColor(ConsoleColor.Yellow))
				Console.Write("Analysing addresses...");

			var results = await new Why(Log).Find(toCC, token, lookFor);

			Console.WriteLine();
			Console.WriteLine();

			if (results == null)
			{
				using (new ConsoleForegroundColor(ConsoleColor.Red))
					Console.WriteLine("Unable to perform the search.");
			}
			else if (results.Count == 0)
			{
				using (new ConsoleForegroundColor(ConsoleColor.Yellow))
					Console.WriteLine("Not found!");
			}
			else
			{
				using (new ConsoleForegroundColor(ConsoleColor.Yellow))
					Console.WriteLine("All the hits");

				using (new ConsoleForegroundColor(ConsoleColor.Green))
				{
					foreach (var item in results)
						Console.WriteLine(item);
				}
			}
		}

		private static void Log(ISearchEvent evt)
		{
			switch (evt)
			{
				case MemberFoundEvent m:
					Console.WriteLine();
					using (new ConsoleForegroundColor(ConsoleColor.White))
						Console.Write(new string('·', m.Level - 1));
					using (new ConsoleForegroundColor(ConsoleColor.Cyan))
					{
						if (m.DisplayName != null)
							Console.Write($"{m.DisplayName} <{m.Email}>");
						else
							Console.Write(m.Email);
					}
					break;
				case MemberNotFoundEvent m:
					Console.WriteLine();
					using (new ConsoleForegroundColor(ConsoleColor.White))
						Console.Write(new string('·', m.Level - 1));
					using (new ConsoleForegroundColor(ConsoleColor.Cyan))
						Console.Write(m.Email);
					using (new ConsoleForegroundColor(ConsoleColor.Red))
						Console.Write(" [Not found]");
					break;
				case GroupLoadedEvent g:
					using (new ConsoleForegroundColor(ConsoleColor.Yellow))
						Console.Write($"{new string('\b', " [Loading...]".Length)} [Members: {g.MemberCount}]");
					break;
				case GroupLoadingEvent _:
					using (new ConsoleForegroundColor(ConsoleColor.White))
						Console.Write(" [Loading...]");
					break;
				case MatchFoundEvent _:
					using (new ConsoleForegroundColor(ConsoleColor.Green))
						Console.Write(" [BINGO! Match detected!]");
					break;
				case CycleDetectdEvent c:
					using (new ConsoleForegroundColor(ConsoleColor.Magenta))
						Console.Write($" [Circular membership: {c.Email}]");
					break;
				case ErrorEvent e:
					Console.WriteLine();
					using (new ConsoleForegroundColor(ConsoleColor.Red))
						Console.Write($"Http Error {e.StatusCode} - {e.Error}");
					break;
				case ExceptionEvent e:
					Console.WriteLine();
					using (new ConsoleForegroundColor(ConsoleColor.Red))
						Console.Write($"{e.Exception.GetType()} - {e.Error}. {e.Exception.Message}");
					break;
			}
		}

		private class Options
		{
			[Option("tocc", Required = true, HelpText = "Semicolon seprated email list. Eg. --tocc \"foo@bar.com; Named Foo <aaa@bbb.ccc>; test@ing.com\"")]
			public string ToCC { get; set; }

			[Option("token", Required = true, HelpText = "Authentication token. To obtain a token, go to https://developer.microsoft.com/en-us/graph/graph-explorer and sign-in, then use browser Dev Tools to get a token")]
			public string Token { get; set; }

			[Option("lookfor", Required = false, HelpText = "Optional email to search for; defaults to the authenticated user")]
			public string LookFor { get; set; }
		}
	}
}
