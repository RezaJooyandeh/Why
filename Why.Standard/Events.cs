using System;
using System.Net;

namespace Microsoft
{
	public interface ISearchEvent { }

	public class UserLoadedEvent : ISearchEvent
	{
		public string Email { get; }
		public UserLoadedEvent(string email) => Email = email;
	}

	public class MemberFoundEvent : ISearchEvent
	{
		public string Email { get; }
		public string DisplayName { get; }
		public int Level { get; }

		public MemberFoundEvent(string email, string displayName, int level)
		{
			Email = email;
			DisplayName = displayName;
			Level = level;
		}
	}

	public class MemberNotFoundEvent : ISearchEvent
	{
		public string Email { get; }
		public int Level { get; }

		public MemberNotFoundEvent(string email, int level)
		{
			Email = email;
			Level = level;
		}
	}

	public class GroupLoadingEvent : ISearchEvent { }

	public class CycleDetectdEvent : ISearchEvent { }

	public class ErrorEvent : ISearchEvent
	{
		public string Error { get; }
		public HttpStatusCode StatusCode { get; }

		public ErrorEvent(string error, HttpStatusCode statusCode)
		{
			Error = error;
			StatusCode = statusCode;
		}
	}

	public class ExceptionEvent : ISearchEvent
	{
		public string Error { get; }
		public Exception Exception { get; }

		public ExceptionEvent(string error, Exception exception)
		{
			Error = error;
			Exception = exception;
		}
	}

	public class GroupLoadedEvent : ISearchEvent
	{
		public GroupLoadedEvent(int memberCount) => MemberCount = memberCount;

		public int MemberCount { get; }
	}

	public class MatchFoundEvent : ISearchEvent { }
}
