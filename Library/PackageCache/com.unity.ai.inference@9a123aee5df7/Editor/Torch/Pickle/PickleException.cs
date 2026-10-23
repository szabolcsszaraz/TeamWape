/* part of Pickle, by Irmen de Jong (irmen@razorvine.net) */

using System;

namespace Razorvine.Pickle
{
	/// <summary>
	/// Exception thrown when something went wrong with pickling or unpickling.
	/// </summary>
	class PickleException : Exception
	{
		public PickleException(string message) : base(message)
		{
		}

		public PickleException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
