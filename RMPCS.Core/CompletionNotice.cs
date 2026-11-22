namespace RMPCS.Core;

public enum CompletionNotice { Finished = 1, NeedsReview = 2, Stopped = 3, RestartPending = 4, Skipped = 5, Deferred = 6 }
public static class CompletionNotices
{
    public static CompletionNotice From(MaintenanceResult result) => result.RestartScheduled ? CompletionNotice.RestartPending : result.Failed > 0 || result.SkippedEntries > 0 ? CompletionNotice.NeedsReview : result.Stopped ? CompletionNotice.Stopped : CompletionNotice.Finished;
    public static string Text(CompletionNotice notice) => notice switch
    {
        CompletionNotice.Finished => "Maintenance tasks finished. Open RMPCS History and review the run summary and repair output.",
        CompletionNotice.NeedsReview => "Maintenance needs review: a task failed, entries were skipped, or logging was unavailable. Open RMPCS History/Activity.",
        CompletionNotice.Stopped => "Maintenance queue stopped after the active task. Open RMPCS History to review what finished.",
        CompletionNotice.RestartPending => "A restart countdown is active. Save your work, or open RMPCS to cancel the restart.",
        CompletionNotice.Skipped => "Scheduled maintenance was skipped because another maintenance session was active.",
        CompletionNotice.Deferred => "Scheduled maintenance did not start because start-window/workload safeguards prevented it. Open History for the reason.",
        _ => ""
    };
}
