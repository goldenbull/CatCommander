using Xunit;

// Avalonia.Headless tests each drive their own simulated UI thread/dispatcher. xUnit v3
// parallelizes test collections (one per class, by default) across the thread pool, and repeated
// full-suite runs showed a rare (roughly 1 in 4-8 runs), hard-to-reproduce-in-isolation
// "No batch update in progress" InvalidOperationException deep in TreeSelectionModelBase - the
// signature of two AvaloniaFact tests from different classes interfering with each other's
// dispatcher/selection-model state, not a bug in any one test. Serializing collections measurably
// reduced (though didn't fully eliminate) how often it showed up across repeated runs.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
