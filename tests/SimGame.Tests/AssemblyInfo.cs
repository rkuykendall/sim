using Xunit;

// Disable parallel test execution because tests share static ContentDatabase state
[assembly: CollectionBehavior(DisableTestParallelization = true)]
