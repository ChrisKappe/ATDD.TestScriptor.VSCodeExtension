using ALObjectParser.Library;
using Newtonsoft.Json;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ATDD.TestScriptor.Library
{
    public class ALTestCodeunitReader : ALObjectReaderBase
    {
        public ALTestCodeunitReader() : base()
        {

        }

        public override void OnRead(IEnumerable<string> Lines, IALObject Target, out IALObject NewTarget)
        {
            base.OnRead(Lines, Target, out NewTarget);
            var str = JsonConvert.SerializeObject(Target);
            
            var TestTarget = new TestALCodeunit
            {
                Id = Target.Id,
                Name = Target.Name,
                Methods = Target.Methods.Select(s => new TestALMethod
                {
                    Attributes = s.Attributes,
                    Comments = s.Comments,
                    Content = s.Content,
                    IsLocal = s.IsLocal,
                    MethodBody = s.MethodBody,
                    MethodKind = s.MethodKind,
                    Name = s.Name,
                    Parameters = s.Parameters,
                    ReturnTypeDefinition = s.ReturnTypeDefinition,
                    TestMethod = s.TestMethod,
                    MethodRange = s.MethodRange
                }).ToList(),
                GlobalVariables = Target.GlobalVariables,
                Properties = Target.Properties,
                Type = Target.Type,
                Comments = Target.Comments,
                Sections = Target.Sections
            };

            if (TestTarget.Methods == null)
                return;

            if (TestTarget.Methods.Count == 0)
                return;

            var testMethods = TestTarget.Methods.Where(w => w.TestMethod == true).ToList();
            var enumNames = Enum.GetNames(typeof(ScenarioElementType));
            var enumNamesJoined = String.Join('|', enumNames);
            var pattern = @".*\[(" + enumNamesJoined + @")(.*?)\]\s+(.*)";
            var features = new List<ITestFeature>();

            foreach (var method in testMethods)
            {
                var matches = Regex.Matches(method.Content, pattern, RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    var matchList = matches.ToList();
                    var scenarioElems = matchList
                        .Select(m => new
                        {
                            Type = m.Groups[1].Value.ToEnum<ScenarioElementType>(),
                            Id = m.Groups[2].Value.Trim().Replace("#", ""),
                            Name = m.Groups[3].Value.Trim(),
                            LineText = m.Groups[0].Value.Trim()
                        });

                    ITestFeature feature = new TestFeature();
                    var scenario = new TestScenario();

                    foreach (var elem in scenarioElems)
                    {
                        switch (elem.Type)
                        {
                            case ScenarioElementType.FEATURE:
                                feature = features.FirstOrDefault(a => a.Name == elem.Name);
                                if (feature == null)
                                {
                                    feature = new TestFeature
                                    {
                                        Name = elem.Name,
                                        Scenarios = new List<ITestScenario>()
                                    };
                                    features.Add(feature);
                                }
                                break;
                            case ScenarioElementType.SCENARIO:
                                scenario = new TestScenario();
                                scenario.ID = int.Parse(elem.Id);
                                scenario.Name = elem.Name;
                                scenario.MethodName = method.Name;
                                scenario.Feature = feature;
                                feature.Scenarios.Add(scenario);
                                break;
                            case ScenarioElementType.GIVEN:
                                var given = new TestScenarioElement();
                                given.Type = elem.Type;
                                given.Value = elem.Name;
                                given.LineText = elem.LineText;
                                scenario.Elements.Add(given);
                                break;
                            case ScenarioElementType.WHEN:
                                var when = new TestScenarioElement();
                                when.Type = elem.Type;
                                when.Value = elem.Name;
                                when.LineText = elem.LineText;
                                scenario.Elements.Add(when);
                                break;
                            case ScenarioElementType.THEN:
                                var then = new TestScenarioElement();
                                then.Type = elem.Type;
                                then.Value = elem.Name;
                                then.LineText = elem.LineText;
                                scenario.Elements.Add(then);
                                break;
                            default:
                                break;
                        }
                    }

                    method.Scenario = scenario;
                }
            }

            TestTarget.Features = features;
            NewTarget = TestTarget;
        }
    }

    /*    public class ALTestCodeunitParser : ALObjectParser
        {
            public ALTestCodeunitParser(): base()
            {
                ALObject = new ALCodeunit();
            }

            public ALTestCodeunitParser(string FilePath) : base(FilePath)
            {
                ALObject = new ALCodeunit();
                Path = FilePath;
            }

            public ALTestCodeunitParser(ALParserConfig config) : base(config)
            {
                ALObject = new ALCodeunit();
            }

            #region Read from Object

            public override void OnRead(List<string> Lines, IALObject Target)
            {
                if (Target.Methods == null)
                    return;

                if (Target.Methods.Count == 0)
                    return;

                var testMethods = Target.Methods.Where(w => w.TestMethod == true).ToList();
                var pattern = @"\[([aA-zZ]+)(.*)\]\s+(.*)";
                var features = new List<ITestFeature>();

                foreach (var method in testMethods)
                {
                    var matches = Regex.Matches(method.Content, pattern);
                    if (matches.Count > 0)
                    {
                        var matchList = matches.ToList();
                        var scenarioElems = matchList
                            .Select(m => new { 
                                Type = m.Groups[1].Value.ToEnum<ScenarioElementType>(), 
                                Id = m.Groups[2].Value.Trim().Replace("#", ""),
                                Name = m.Groups[3].Value.Trim() 
                            });

                        ITestFeature feature = new TestFeature();
                        var scenario = new TestScenario();

                        foreach (var elem in scenarioElems)
                        {
                            switch (elem.Type)
                            {
                                case ScenarioElementType.FEATURE:
                                    feature = features.FirstOrDefault(a => a.Name == elem.Name);
                                    if (feature == null)
                                    {
                                        feature = new TestFeature {
                                            Name = elem.Name,
                                            Scenarios = new List<ITestScenario>()
                                        };
                                        features.Add(feature);
                                    }
                                    break;
                                case ScenarioElementType.SCENARIO:
                                    scenario = new TestScenario();
                                    scenario.ID = int.Parse(elem.Id);
                                    scenario.Name = elem.Name;
                                    scenario.Feature = feature;
                                    feature.Scenarios.Add(scenario);
                                    break;
                                case ScenarioElementType.GIVEN:
                                    var given = new TestScenarioElement();
                                    given.Type = elem.Type;
                                    given.Value = elem.Name;
                                    scenario.Elements.Add(given);
                                    break;
                                case ScenarioElementType.WHEN:
                                    var when = new TestScenarioElement();
                                    when.Type = elem.Type;
                                    when.Value = elem.Name;
                                    scenario.Elements.Add(when);
                                    break;
                                case ScenarioElementType.THEN:
                                    var then = new TestScenarioElement();
                                    then.Type = elem.Type;
                                    then.Value = elem.Name;
                                    scenario.Elements.Add(then);
                                    break;
                                default:
                                    break;
                            }
                        }

                        method.Scenario = scenario;
                    }
                }

                Target.Features = features;
            }

            #endregion

            #region Write to Object

            public override void OnWriteObjectHeader(IndentedTextWriter writer, IALObject Target, List<ITestFeature> Features = null)
            {
                base.OnWriteObjectHeader(writer, Target, Features);
                writer.Indent++;
                writer.WriteLine("SubType = Test;");
                writer.WriteLine();
                writer.Indent--;
            }

            public override void OnWriteObjectMethods(IndentedTextWriter writer, IALObject Target, List<ITestFeature> Features = null)
            {
                if (Features != null && Features.Count() > 0)
                {
                    if (Target.Methods.Count > 0)
                    {
                        MergeFeatures(Target, Features);
                    }
                    else
                    {
                        FeaturesToMethods(Target, Features);
                    }
                }

                base.OnWriteObjectMethods(writer, Target, Features);
            }

            public override string OnWriteObjectMethod(IALObject Target, ALMethod method)
            {
                var result = "";
                using (var stringWriter = new StringWriter())
                {
                    using (var writer = new IndentedTextWriter(stringWriter))
                    {
                        writer.Indent++;
                        writer.WriteLine();

                        WriteObjectMethodHeader(method, writer);
                        WriteObjectMethodBody(Target, method, writer);
                        WriteObjectMethodFooter(method, writer);

                        writer.Indent--;

                        result = stringWriter.ToString().Replace("}", "").Trim();
                    }
                }

                return result;
            }

            public void WriteObjectMethodHeader(ALMethod method, IndentedTextWriter writer)
            {
                bool HasScenario = method.Scenario != null;
                if (HasScenario)
                {
                    writer.WriteLine($"//#region [SCENARIO #{method.Scenario.ID:0000}] {method.Scenario.Name}");
                    writer.WriteLine();
                }

                if (method.TestMethod)
                {
                    writer.WriteLine("[Test]");
                }

                var parameterTxt = "";
                if (method.Parameters.Count > 0)
                {
                    parameterTxt = String.Join(';', method.Parameters.Select(s => $"{(s.IsVar ? "var " : "")}{s.Name}: {s.Type}"));
                }

                writer.WriteLine($"{(method.IsLocal ? "local " : "")}{method.MethodKind} {method.Name}({parameterTxt}){(!String.IsNullOrEmpty(method.ReturnType) ? ": " + method.ReturnType : "")}");
            }

            public void WriteObjectMethodBody(IALObject Target, ALMethod method, IndentedTextWriter writer)
            {
                bool HasScenario = method.Scenario != null;
                bool NoContent = String.IsNullOrEmpty(method.Content);

                if (NoContent)
                {
                    if (HasScenario)
                    {
                        writer.WriteLine(method.Scenario.Feature.Write());
                    }

                    writer.WriteLine("begin");

                    if (HasScenario)
                    {
                        writer.Indent++;

                        writer.WriteLine(method.Scenario.Write());
                        writer.WriteLine("Initialize();");
                        if (method.Scenario.Elements != null)
                        {
                            writer.WriteLine();
                            method.Scenario
                                .Elements
                                .OrderBy(o => o.Type)
                                .ToList()
                                .ForEach(e => {
                                    writer.WriteLine(e.Write());
                                    writer.WriteLine(e.WriteMethod(Config) + "();");
                                    writer.WriteLine();
                                });
                        }

                        writer.Indent--;
                    }

                    writer.WriteLine("end;");

                    method.Scenario
                        .Elements
                        .OrderBy(o => o.Type)
                        .ToList()
                        .ForEach(e =>
                        {
                            if (!Target.Methods.Any(a => a.Name == e.WriteMethod(Config)))
                            {
                                writer.WriteLine();
                                writer.WriteLine($"local procedure {e.WriteMethod(Config)}()");
                                writer.WriteLine("begin");
                                writer.WriteLine("end;");
                            }
                        });
                }
                else
                {
                    writer.WriteLine(method.Content);
                }
            }

            public void WriteObjectMethodFooter(ALMethod method, IndentedTextWriter writer)
            {
                bool HasScenario = method.Scenario != null;
                if (HasScenario)
                {
                    writer.WriteLine();
                    writer.WriteLine("//#endregion");
                }
            }

            #endregion

            #region Merge Feature-sets or create a new set

            public void FeaturesToMethods(IALObject Target, List<ITestFeature> Features = null)
            {
                if (Features == null)
                    return;

                if (Features.Count == 0)
                    return;

                Target.Methods.AddRange(Features
                    .SelectMany(s => s.Scenarios)
                    .Select(s => new ALMethod()
                    {
                        Name = s.Name.SanitizeName(),
                        TestMethod = true,
                        Scenario = s,
                        MethodKind = "procedure",
                        Content = ""
                    })
                    .ToList()
                );
            }

            public void MergeFeatures(IALObject Target, List<ITestFeature> Features = null)
            {
                if (Features == null)
                    return;

                if (Features.Count == 0)
                    return;

                var identical = Target.Features.SequenceEqual(Features, new TestFeatureComparer());
                if (identical)
                {
                    return;
                }

                // add new features
                var newFeatures = Features.Except(Target.Features, new TestFeatureNameComparer()).ToList();
                if (newFeatures.Count() > 0)
                {
                    Target.Features.AddRange(newFeatures);

                    FeaturesToMethods(Target, newFeatures);
                }

                // check same feature for new scenarios
                foreach(var feature in Target.Features)
                {
                    var UpdatedFeature = Features.FirstOrDefault(f => f.Name == feature.Name);
                    if (UpdatedFeature != null)
                    {
                        var newScenarios = UpdatedFeature.Scenarios.Except(feature.Scenarios, new TestScenarioIDComparer()).ToList();
                        if (newScenarios.Count > 0)
                        {
                            (feature.Scenarios as List<ITestScenario>).AddRange(newScenarios);
                            Target.Methods.AddRange(newScenarios
                                .Select(s => new ALMethod()
                                {
                                    Name = s.Name.SanitizeName(),
                                    TestMethod = true,
                                    Scenario = s,
                                    MethodKind = "procedure",
                                    Content = ""
                                })
                                .ToList()
                            );
                        }
                    }
                }

                // check existing scenarios for updates
                var CurrentScenarios = Target.Features.SelectMany(s => s.Scenarios).ToList();
                var UpdatedScenarios = Features.SelectMany(s => s.Scenarios).ToList();

                foreach (var scenario in CurrentScenarios)
                {
                    var UpdatedScenario = UpdatedScenarios
                        .Where(w => w.Feature.Name == scenario.Feature.Name && w.ID == scenario.ID)
                        .FirstOrDefault();

                    if (UpdatedScenario != null)
                    {
                        scenario.Name = UpdatedScenario.Name;
                        scenario.ID = UpdatedScenario.ID;
                        scenario.Elements = UpdatedScenario.Elements.ToList();

                        var method = Target.Methods.Where(w => w.Scenario == scenario).FirstOrDefault();
                        if (method != null)
                        {
                            method.Scenario = UpdatedScenario;
                            method.Content = ""; //TODO!! Update content instead of recreation
                        }
                    }
                }
            }

            #endregion
        }*/
}
