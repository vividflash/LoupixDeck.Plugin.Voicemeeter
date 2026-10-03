global using Xunit;

// Localization keeps the plugin host in a static field; running tests one at a time keeps a
// translating fake host from leaking into other tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
