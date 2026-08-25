using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;

static class TestProbe
{
    [InitializeOnLoadMethod]
    static void Register()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            assemblyNames = new[] { "CatShelter.Core.Tests" }
        };
        api.RegisterCallbacks(new ProbeCallbacks());
        // Defer execution to after editor init completes
        EditorApplication.delayCall += () =>
        {
            Debug.Log("[TestProbe] launching test run via API");
            api.Execute(new ExecutionSettings(filter));
        };
    }
}

class ProbeCallbacks : ICallbacks
{
    public void RunStarted(ITestAdaptor tests)
        => Debug.Log($"[TestProbe] RunStarted: {tests.TestCaseCount} test cases, children={tests.Children.Count()}");
    public void RunFinished(ITestResultAdaptor result)
        => Debug.Log($"[TestProbe] RunFinished: {result.TestStatus}, cases={result.Test.TestCaseCount}");
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result)
    {
        if (result.Test.IsSuite) return;
        Debug.Log($"[TestProbe]   {(result.TestStatus == TestStatus.Passed ? "PASS" : result.TestStatus)}: {result.Test.FullName}");
    }
}
