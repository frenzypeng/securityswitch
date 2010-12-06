﻿using System.Text.RegularExpressions;


namespace SecuritySwitch {
	/// <summary>
	/// An implementation of IPathMatcher that matches the pattern as a regex against the path; accounting for variances in case if indicated.
	/// </summary>
	public class RegexPathMatcher : IPathMatcher {
		/// <summary>
		/// Determines whether the specified path is a match to the provided pattern.
		/// </summary>
		/// <param name="path">The path.</param>
		/// <param name="pattern">The pattern to match against.</param>
		/// <param name="ignoreCase">A flag that indicates whether or not to ignore the case of the path and pattern when matching.</param>
		/// <returns>
		/// 	<c>true</c> if the specified path is a match with the pattern; otherwise, <c>false</c>.
		/// </returns>
		public bool IsMatch(string path, string pattern, bool ignoreCase) {
			const RegexOptions Options = (RegexOptions.CultureInvariant | RegexOptions.Singleline);
			return Regex.IsMatch(path, pattern, (ignoreCase ? Options | RegexOptions.IgnoreCase : Options));
		}
	}
}