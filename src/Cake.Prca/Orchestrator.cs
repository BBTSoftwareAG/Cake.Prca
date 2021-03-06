﻿namespace Cake.Prca
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using Core.Diagnostics;
    using Issues;
    using PullRequests;

    /// <summary>
    /// Class for writing code analysis issues to pull requests.
    /// </summary>
    internal class Orchestrator
    {
        private readonly ICakeLog log;
        private readonly List<ICodeAnalysisProvider> codeAnalysisProviders = new List<ICodeAnalysisProvider>();
        private readonly IPullRequestSystem pullRequestSystem;
        private readonly ReportIssuesToPullRequestSettings settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="Orchestrator"/> class.
        /// </summary>
        /// <param name="log">Cake log instance.</param>
        /// <param name="codeAnalysisProviders">List of code analysis issue providers to use.</param>
        /// <param name="pullRequestSystem">Object for accessing pull request system.
        /// <c>null</c> if only issues should be read.</param>
        /// <param name="settings">Settings.</param>
        public Orchestrator(
            ICakeLog log,
            IEnumerable<ICodeAnalysisProvider> codeAnalysisProviders,
            IPullRequestSystem pullRequestSystem,
            ReportIssuesToPullRequestSettings settings)
        {
            log.NotNull(nameof(log));
            pullRequestSystem.NotNull(nameof(pullRequestSystem));
            settings.NotNull(nameof(settings));

            // ReSharper disable once PossibleMultipleEnumeration
            codeAnalysisProviders.NotNullOrEmptyOrEmptyElement(nameof(codeAnalysisProviders));

            this.log = log;
            this.pullRequestSystem = pullRequestSystem;
            this.settings = settings;

            // ReSharper disable once PossibleMultipleEnumeration
            this.codeAnalysisProviders.AddRange(codeAnalysisProviders);
        }

        /// <summary>
        /// Runs the orchestrator.
        /// Posts new issues, ignoring duplicate comments and resolves comments that were open in an old iteration
        /// of the pull request.
        /// </summary>
        /// <returns>Information about the reported and written issues.</returns>
        public PrcaResult Run()
        {
            var format = PrcaCommentFormat.Undefined;

            // Initialize pull request system.
            this.log.Verbose("Initialize pull request system...");
            var pullRequestSystemInitialized = this.pullRequestSystem.Initialize(this.settings);
            if (pullRequestSystemInitialized)
            {
                format = this.pullRequestSystem.GetPreferredCommentFormat();
                this.log.Verbose("Pull request system prefers comments in {0} format.", format);
            }
            else
            {
                this.log.Warning("Error initializing the pull request system.");
            }

            var issueReader =
                new IssueReader(this.log, this.codeAnalysisProviders, this.settings);
            var issues = issueReader.ReadIssues(format).ToList();

            // Don't process issues if pull request system could not be initialized.
            if (!pullRequestSystemInitialized)
            {
                return new PrcaResult(issues, new List<ICodeAnalysisIssue>());
            }

            this.log.Information("Processing {0} new issues", issues.Count);
            var postedIssues = this.PostAndResolveComments(this.settings, issues);

            return new PrcaResult(issues, postedIssues);
        }

        /// <summary>
        /// Checks if file path from an <see cref="ICodeAnalysisIssue"/> and <see cref="IPrcaDiscussionThread"/>
        /// are matching.
        /// </summary>
        /// <param name="issue">Issue to check.</param>
        /// <param name="thread">Comment thread to check.</param>
        /// <returns><c>true</c> if both paths are matching or if both paths are set to <c>null</c>.</returns>
        private static bool FilePathsAreMatching(ICodeAnalysisIssue issue, IPrcaDiscussionThread thread)
        {
            return
                (issue.AffectedFileRelativePath == null && thread.AffectedFileRelativePath == null) ||
                (
                    issue.AffectedFileRelativePath != null &&
                    thread.AffectedFileRelativePath != null &&
                    thread.AffectedFileRelativePath.ToString() == issue.AffectedFileRelativePath.ToString());
        }

        /// <summary>
        /// Posts new issues, ignoring duplicate comments and resolves comments that were open in an old iteration
        /// of the pull request.
        /// </summary>
        /// <param name="reportIssuesToPullRequestSettings">Settings for posting the issues.</param>
        /// <param name="issues">Issues to post.</param>
        /// <returns>Issues reported to the pull request.</returns>
        private IEnumerable<ICodeAnalysisIssue> PostAndResolveComments(
            ReportIssuesToPullRequestSettings reportIssuesToPullRequestSettings,
            IList<ICodeAnalysisIssue> issues)
        {
            issues.NotNull(nameof(issues));

            this.log.Information("Fetching existing threads and comments...");

            var existingThreads =
                this.pullRequestSystem.FetchActiveDiscussionThreads(
                    reportIssuesToPullRequestSettings.CommentSource).ToList();

            var issueComments =
                this.BuildIssueToCommentDictonary(
                    reportIssuesToPullRequestSettings,
                    issues,
                    existingThreads);

            // Comments that were created by this logic but do not have corresponding issues can be marked as 'Resolved'
            this.ResolveExistingComments(existingThreads, issueComments);

            if (!issues.Any())
            {
                this.log.Information("No new issues were posted");
                return new List<ICodeAnalysisIssue>();
            }

            // Remove issues that cannot be posted
            var issueFilterer =
                new IssueFilterer(this.log, this.pullRequestSystem, reportIssuesToPullRequestSettings);
            var remainingIssues = issueFilterer.FilterIssues(issues, issueComments).ToList();

            if (remainingIssues.Any())
            {
                var formattedMessages =
                    from issue in remainingIssues
                    select
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "  Rule: {0} Line: {1} File: {2}",
                            issue.Rule,
                            issue.Line,
                            issue.AffectedFileRelativePath);

                this.log.Verbose(
                    "Posting {0} issue(s):\n{1}",
                    remainingIssues.Count,
                    string.Join(Environment.NewLine, formattedMessages));

                this.pullRequestSystem.PostDiscussionThreads(
                    remainingIssues,
                    reportIssuesToPullRequestSettings.CommentSource);
            }
            else
            {
                this.log.Information("All issues were filtered. Nothing new to post.");
            }

            return remainingIssues;
        }

        /// <summary>
        /// Returns existing matching comments from the pull request for a list of issues.
        /// </summary>
        /// <param name="reportIssuesToPullRequestSettings">Settings to use.</param>
        /// <param name="issues">Issues for which matching comments should be found.</param>
        /// <param name="existingThreads">Existing discussion threads on the pull request.</param>
        /// <returns>Dictionary containing issues and its associated matching comments on the pull request.</returns>
        private IDictionary<ICodeAnalysisIssue, IEnumerable<IPrcaDiscussionComment>> BuildIssueToCommentDictonary(
            ReportIssuesToPullRequestSettings reportIssuesToPullRequestSettings,
            IList<ICodeAnalysisIssue> issues,
            IList<IPrcaDiscussionThread> existingThreads)
        {
            issues.NotNull(nameof(issues));
            existingThreads.NotNull(nameof(existingThreads));

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var result = new Dictionary<ICodeAnalysisIssue, IEnumerable<IPrcaDiscussionComment>>();
            foreach (var issue in issues)
            {
                var matchingComments =
                    this.GetMatchingComments(
                        reportIssuesToPullRequestSettings,
                        issue,
                        existingThreads).ToList();

                if (matchingComments.Any())
                {
                    result.Add(issue, matchingComments);
                }
            }

            this.log.Verbose("Built a issue to comment dictionary in {0} ms", stopwatch.ElapsedMilliseconds);

            return result;
        }

        /// <summary>
        /// Returns all matching comments from discussion threads for an issue.
        /// Comments are considered matching if they fulfill all of the following conditions:
        /// * The thread is active.
        /// * The thread is for the same file.
        /// * The thread was created by the same logic, i.e. the same <code>commentSource</code>.
        /// * The comment contains the same content.
        /// </summary>
        /// <remarks>
        /// The line cannot be used since comments can move around.
        /// </remarks>
        /// <param name="reportIssuesToPullRequestSettings">Settings to use.</param>
        /// <param name="issue">Issue for which the comments should be returned.</param>
        /// <param name="existingThreads">Existing discussion threads on the pull request.</param>
        /// <returns>Active comments for the issue.</returns>
        private IEnumerable<IPrcaDiscussionComment> GetMatchingComments(
            ReportIssuesToPullRequestSettings reportIssuesToPullRequestSettings,
            ICodeAnalysisIssue issue,
            IList<IPrcaDiscussionThread> existingThreads)
        {
            issue.NotNull(nameof(issue));
            existingThreads.NotNull(nameof(existingThreads));

            // Select threads that are active, that point to the same file and have been marked with the given comment source.
            var matchingThreads =
                (from thread in existingThreads
                where
                    thread != null &&
                    thread.Status == PrcaDiscussionStatus.Active &&
                    FilePathsAreMatching(issue, thread) &&
                    thread.CommentSource == reportIssuesToPullRequestSettings.CommentSource
                select thread).ToList();

            if (matchingThreads.Any())
            {
                this.log.Verbose(
                    "Found {0} matching thread(s) for the issue at {1} line {2}",
                    matchingThreads.Count,
                    issue.AffectedFileRelativePath,
                    issue.Line);
            }

            var result = new List<IPrcaDiscussionComment>();
            foreach (var thread in matchingThreads)
            {
                // Select comments from this thread that are not deleted and that match the given message.
                var matchingComments =
                    (from comment in thread.Comments
                    where
                        comment != null &&
                        !comment.IsDeleted &&
                        comment.Content == issue.Message
                    select
                        comment).ToList();

                if (matchingComments.Any())
                {
                    this.log.Verbose(
                        "Found {0} matching comment(s) for the issue at {1} line {2}",
                        matchingComments.Count,
                        issue.AffectedFileRelativePath,
                        issue.Line);
                }

                result.AddRange(matchingComments);
            }

            return result;
        }

        /// <summary>
        /// Marks comment threads created by this logic but without active issues as resolved.
        /// </summary>
        /// <param name="existingThreads">Existing discussion threads on the pull request.</param>
        /// <param name="issueComments">Issues and their related comments.</param>
        private void ResolveExistingComments(
            IList<IPrcaDiscussionThread> existingThreads,
            IDictionary<ICodeAnalysisIssue, IEnumerable<IPrcaDiscussionComment>> issueComments)
        {
            existingThreads.NotNull(nameof(existingThreads));
            issueComments.NotNull(nameof(issueComments));

            if (!existingThreads.Any())
            {
                this.log.Verbose("No existings threads to resolve.");
                return;
            }

            var resolvedThreads =
                this.GetResolvedThreads(existingThreads, issueComments).ToList();

            this.log.Verbose("Mark {0} threads as fixed...", resolvedThreads.Count);
            this.pullRequestSystem.MarkThreadsAsFixed(resolvedThreads);
        }

        /// <summary>
        /// Returns threads that can be resolved.
        /// </summary>
        /// <param name="existingThreads">Existing discussion threads on the pull request.</param>
        /// <param name="issueComments">Issues and their related comments.</param>
        /// <returns>List of threads which can be resolved.</returns>
        private IEnumerable<IPrcaDiscussionThread> GetResolvedThreads(
            IList<IPrcaDiscussionThread> existingThreads,
            IDictionary<ICodeAnalysisIssue, IEnumerable<IPrcaDiscussionComment>> issueComments)
        {
            existingThreads.NotNull(nameof(existingThreads));
            issueComments.NotNull(nameof(issueComments));

            var currentComments = new HashSet<IPrcaDiscussionComment>(issueComments.Values.SelectMany(x => x));

            var result =
                existingThreads.Where(thread => !thread.Comments.Any(x => currentComments.Contains(x))).ToList();

            this.log.Verbose(
                "Found {0} existing thread(s) that do not match any new issue and can be resolved.",
                result.Count);

            return result;
        }
    }
}
