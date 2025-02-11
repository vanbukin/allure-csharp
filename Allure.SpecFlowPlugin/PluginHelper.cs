﻿using Allure.Net.Commons;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Bindings;

namespace Allure.SpecFlowPlugin
{
  public static class PluginHelper
  {
    public static string IGNORE_EXCEPTION = "IgnoreException";
    private static readonly ScenarioInfo emptyScenarioInfo = new ScenarioInfo("Unknown", string.Empty, Array.Empty<string>(), new OrderedDictionary());

    private static readonly FeatureInfo emptyFeatureInfo = new FeatureInfo(
        CultureInfo.CurrentCulture, string.Empty, string.Empty, string.Empty);

    internal static PluginConfiguration PluginConfiguration =
        GetConfiguration(AllureLifecycle.Instance.JsonConfiguration);

    public static PluginConfiguration GetConfiguration(string allureConfiguration)
    {
      var config = new PluginConfiguration();
      var specflowSection = JObject.Parse(allureConfiguration)["specflow"];
      if (specflowSection != null)
        config = specflowSection.ToObject<PluginConfiguration>();
      return config;
    }

    internal static string GetFeatureContainerId(FeatureInfo featureInfo)
    {
      var id = featureInfo != null
          ? featureInfo.GetHashCode().ToString()
          : emptyFeatureInfo.GetHashCode().ToString();

      return id;
    }

    internal static string NewId()
    {
      return Guid.NewGuid().ToString("N");
    }

    internal static FixtureResult GetFixtureResult(HookBinding hook)
    {
      return new FixtureResult
      {
        name = $"{hook.Method.Name} [{hook.HookOrder}]"
      };
    }

    internal static TestResult StartTestCase(string containerId, FeatureContext featureContext,
        ScenarioContext scenarioContext)
    {
      var featureInfo = featureContext?.FeatureInfo ?? emptyFeatureInfo;
      var scenarioInfo = scenarioContext?.ScenarioInfo ?? emptyScenarioInfo;
      var tags = GetTags(featureInfo, scenarioInfo);
      var parameters = GetParameters(scenarioInfo);
      var title = scenarioInfo.Title;
      var testResult = new TestResult
      {
        uuid = NewId(),
        historyId = title + parameters.hash,
        name = title,
        fullName = title,
        labels = new List<Label>
                {
                    Label.Thread(),
                    string.IsNullOrWhiteSpace(AllureLifecycle.Instance.AllureConfiguration.Title)
                            ? Label.Host()
                            : Label.Host(AllureLifecycle.Instance.AllureConfiguration.Title),
                    Label.Feature(featureInfo.Title)
                }
              .Union(tags.Item1).ToList(),
        links = tags.Item2,
        parameters = parameters.parameters
      };

      AllureLifecycle.Instance.StartTestCase(containerId, testResult);
      scenarioContext?.Set(testResult);
      featureContext?.Get<HashSet<TestResult>>().Add(testResult);

      return testResult;
    }

    internal static TestResult GetCurrentTestCase(ScenarioContext context)
    {
      context.TryGetValue(out TestResult testresult);
      return testresult;
    }

    internal static TestResultContainer StartTestContainer(FeatureContext featureContext,
        ScenarioContext scenarioContext)
    {
      var containerId = GetFeatureContainerId(featureContext?.FeatureInfo);

      var scenarioContainer = new TestResultContainer
      {
        uuid = NewId()
      };
      AllureLifecycle.Instance.StartTestContainer(containerId, scenarioContainer);
      scenarioContext?.Set(scenarioContainer);
      featureContext?.Get<HashSet<TestResultContainer>>().Add(scenarioContainer);

      return scenarioContainer;
    }

    internal static TestResultContainer GetCurrentTestConainer(ScenarioContext context)
    {
      context.TryGetValue(out TestResultContainer testresultContainer);
      return testresultContainer;
    }

    internal static StatusDetails GetStatusDetails(Exception ex)
    {
      return new StatusDetails
      {
        message = GetFullExceptionMessage(ex),
        trace = ex.ToString()
      };
    }

    private static string GetFullExceptionMessage(Exception ex)
    {
      return ex.Message +
             (!string.IsNullOrWhiteSpace(ex.InnerException?.Message)
                 ? $" -> {GetFullExceptionMessage(ex.InnerException)}"
                 : string.Empty);
    }

    private static Tuple<List<Label>, List<Link>> GetTags(FeatureInfo featureInfo, ScenarioInfo scenarioInfo)
    {
      var result = Tuple.Create(new List<Label>(), new List<Link>());

      var tags = scenarioInfo.Tags
          .Union(featureInfo.Tags)
          .Distinct(StringComparer.CurrentCultureIgnoreCase);

      foreach (var tag in tags)
      {
        var tagValue = tag;
        // link
        if (TryUpdateValueByMatch(PluginConfiguration.links.link, ref tagValue))
        {
          result.Item2.Add(new Link { name = tagValue, url = tagValue });
          continue;
        }

        // issue
        if (TryUpdateValueByMatch(PluginConfiguration.links.issue, ref tagValue))
        {
          result.Item2.Add(Link.Issue(tagValue, tagValue));
          continue;
        }

        // tms
        if (TryUpdateValueByMatch(PluginConfiguration.links.tms, ref tagValue))
        {
          result.Item2.Add(Link.Tms(tagValue, tagValue));
          continue;
        }

        // parent suite
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.suites.parentSuite, ref tagValue))
        {
          result.Item1.Add(Label.ParentSuite(tagValue));
          continue;
        }

        // suite
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.suites.suite, ref tagValue))
        {
          result.Item1.Add(Label.Suite(tagValue));
          continue;
        }

        // sub suite
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.suites.subSuite, ref tagValue))
        {
          result.Item1.Add(Label.SubSuite(tagValue));
          continue;
        }

        // epic
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.behaviors.epic, ref tagValue))
        {
          result.Item1.Add(Label.Epic(tagValue));
          continue;
        }

        // story
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.behaviors.story, ref tagValue))
        {
          result.Item1.Add(Label.Story(tagValue));
          continue;
        }

        // package
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.packages.package, ref tagValue))
        {
          result.Item1.Add(Label.Package(tagValue));
          continue;
        }

        // test class
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.packages.testClass, ref tagValue))
        {
          result.Item1.Add(Label.TestClass(tagValue));
          continue;
        }

        // test method
        if (TryUpdateValueByMatch(PluginConfiguration.grouping.packages.testMethod, ref tagValue))
        {
          result.Item1.Add(Label.TestMethod(tagValue));
          continue;
        }

        // owner
        if (TryUpdateValueByMatch(PluginConfiguration.labels.owner, ref tagValue))
        {
          result.Item1.Add(Label.Owner(tagValue));
          continue;
        }

        // severity
        if (TryUpdateValueByMatch(PluginConfiguration.labels.severity, ref tagValue) &&
            Enum.TryParse(tagValue, out SeverityLevel level))
        {
          result.Item1.Add(Label.Severity(level));
          continue;
        }

        // label
        if (GetLabelProps(PluginConfiguration.labels.label, tagValue, out var props))
        {
          result.Item1.Add(new Label { name = props.Key, value = props.Value});
          continue;
        }
        
        // tag
        result.Item1.Add(Label.Tag(tagValue));
      }

      return result;
    }

    private static (List<Parameter> parameters, string hash) GetParameters(ScenarioInfo scenarioInfo)
    {
      var sb = new StringBuilder();
      var parameters = new List<Parameter>();
      var argumentsEnumerator = scenarioInfo.Arguments.GetEnumerator();
      while (argumentsEnumerator.MoveNext())
      {
        sb.Append(argumentsEnumerator.Key.ToString());
        sb.Append(argumentsEnumerator.Value.ToString());

        parameters.Add(new Parameter { name = argumentsEnumerator.Key.ToString(), value = argumentsEnumerator.Value.ToString() });
      }
      var hash = (parameters.Count > 0) ? sb.ToString().GetDeterministicHashCode().ToString() : string.Empty;
      return (parameters, hash);
    }

    private static bool TryUpdateValueByMatch(string expression, ref string value)
    {
      var matchedGroups = GetMatchedGroups(expression, value);

      if (!matchedGroups.Any()) return false;
      
      if (matchedGroups.Count == 1)
        value = matchedGroups[0];
      else
        value = matchedGroups[1];
      return true;
    }

    private static bool GetLabelProps(string expression, string value, out KeyValuePair<string, string> props)
    {
      props = default;
       
      var matchedGroups = GetMatchedGroups(expression, value);

      if (matchedGroups.Count != 3)
        return false;
      
      props = new KeyValuePair<string, string>(matchedGroups[1], matchedGroups[2]);
      return true;

    }
    
    private static List<string> GetMatchedGroups(string expression, string value)
    {
      var matchedGroups = new List<string>();
      if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(expression))
        return matchedGroups;

      Regex regex = null;
      try
      {
        regex = new Regex(expression,
          RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
      }
      catch (Exception)
      {
        return matchedGroups;
      }

      if (regex == null)
        return matchedGroups;

      if (regex.IsMatch(value))
      {
        var groups = regex.Match(value).Groups;

        for (var i = 0; i < groups.Count; i++)
        {
          matchedGroups.Add(groups[i].Value);
        }

        return matchedGroups;
      }

      return matchedGroups;
    }
    
    private static int GetDeterministicHashCode(this string str)
    {
      unchecked
      {
        int hash1 = (5381 << 16) + 5381;
        int hash2 = hash1;

        for (int i = 0; i < str.Length; i += 2)
        {
          hash1 = ((hash1 << 5) + hash1) ^ str[i];
          if (i == str.Length - 1)
            break;
          hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
        }

        return hash1 + (hash2 * 1566083941);
      }
    }
  }
}
