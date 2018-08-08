﻿using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gherkin.Ast;
using TechTalk.SpecFlow.Infrastructure;
using TechTalk.SpecFlow.Bindings;
using TechTalk.SpecFlow.Parser;

namespace TechTalk.SpecFlow.VsIntegration.StepSuggestions
{
    public class BoundInstanceTemplate<TNativeSuggestionItem> : IBoundStepSuggestion<TNativeSuggestionItem>, IStepSuggestionGroup<TNativeSuggestionItem>
    {
        public StepInstanceTemplate<TNativeSuggestionItem> Template { get; private set; }

        private readonly List<BoundStepSuggestions<TNativeSuggestionItem>> matchGroups = new List<BoundStepSuggestions<TNativeSuggestionItem>>(1);
        public ICollection<BoundStepSuggestions<TNativeSuggestionItem>> MatchGroups { get { return matchGroups; } }

        private readonly StepSuggestionList<TNativeSuggestionItem> suggestions;
        public IEnumerable<IBoundStepSuggestion<TNativeSuggestionItem>> Suggestions { get { return suggestions; } }

        public TNativeSuggestionItem NativeSuggestionItem { get; private set; }
        public StepDefinitionType StepDefinitionType { get { return Template.StepDefinitionType; } }

        public BoundInstanceTemplate(StepInstanceTemplate<TNativeSuggestionItem> template, INativeSuggestionItemFactory<TNativeSuggestionItem> nativeSuggestionItemFactory, IEnumerable<IBoundStepSuggestion<TNativeSuggestionItem>> suggestions)
        {
            Template = template;
            this.suggestions = new StepSuggestionList<TNativeSuggestionItem>(nativeSuggestionItemFactory, suggestions);
            NativeSuggestionItem = nativeSuggestionItemFactory.CloneTo(template.NativeSuggestionItem, this);
        }

        public CultureInfo Language
        {
            get { return Template.Language; } 
        }

        public bool Match(IStepDefinitionBinding binding, CultureInfo bindingCulture, bool includeRegexCheck, IStepDefinitionMatchService stepDefinitionMatchService)
        {
            if (binding.StepDefinitionType != StepDefinitionType)
                return false;

            if (suggestions.Count == 0)
                return false;

            return suggestions.Any(i => i.Match(binding, bindingCulture, true, stepDefinitionMatchService));
        }
    }

    public class StepInstanceTemplate<TNativeSuggestionItem> : IStepSuggestion<TNativeSuggestionItem>
    {
        private readonly StepSuggestionList<TNativeSuggestionItem> instances;
        public IEnumerable<IBoundStepSuggestion<TNativeSuggestionItem>> Instances { get { return instances; } }

        public TNativeSuggestionItem NativeSuggestionItem { get; private set; }

        public StepDefinitionType StepDefinitionType { get; private set; }
        internal string StepPrefix { get; private set; }

        public CultureInfo Language { get; private set; }

        public bool Match(IStepDefinitionBinding binding, CultureInfo bindingCulture, bool includeRegexCheck, IStepDefinitionMatchService stepDefinitionMatchService)
        {
            if (binding.StepDefinitionType != StepDefinitionType)
                return false;

            if (instances.Count == 0)
                return false;

            return instances.Any(i => i.Match(binding, bindingCulture, true, stepDefinitionMatchService));
        }

        static private readonly Regex paramRe = new Regex(@"\<(?<param>[^\>]+)\>");

        public StepInstanceTemplate(SpecFlowStep scenarioStep, ScenarioOutline scenarioOutline, SpecFlowDocument specFlowDocument, StepContext stepContext, INativeSuggestionItemFactory<TNativeSuggestionItem> nativeSuggestionItemFactory)
        {
            StepDefinitionType = (StepDefinitionType)scenarioStep.ScenarioBlock;
            Language = stepContext.Language;

            NativeSuggestionItem = nativeSuggestionItemFactory.Create(scenarioStep.Text, StepInstance<TNativeSuggestionItem>.GetInsertionText(scenarioStep), 1, StepDefinitionType.ToString().Substring(0, 1) + "-t", this);
            instances = new StepSuggestionList<TNativeSuggestionItem>(nativeSuggestionItemFactory);
            AddInstances(scenarioStep, scenarioOutline, specFlowDocument, stepContext, nativeSuggestionItemFactory);

            var match = paramRe.Match(scenarioStep.Text);
            StepPrefix = match.Success ? scenarioStep.Text.Substring(0, match.Index) : scenarioStep.Text;
        }

        private void AddInstances(SpecFlowStep scenarioStep, ScenarioOutline scenarioOutline, SpecFlowDocument specFlowDocument, StepContext stepContext, INativeSuggestionItemFactory<TNativeSuggestionItem> nativeSuggestionItemFactory)
        {
            foreach (var exampleSet in scenarioOutline.Examples)
            {
                foreach (var row in exampleSet.TableBody)
                {
                    var replacedText = paramRe.Replace(scenarioStep.Text,
                        match =>
                        {
                            string param = match.Groups["param"].Value;
                            var cells = exampleSet.TableHeader.Cells.ToArray();
                            int headerIndex = Array.FindIndex(cells, c => c.Value.Equals(param));
                            if (headerIndex < 0)
                                return match.Value;
                            return cells[headerIndex].Value;
                        });
                    
                    var newStep = new SpecFlowStep(scenarioStep.Location, 
                        scenarioStep.Keyword, 
                        replacedText, 
                        scenarioStep.Argument, 
                        scenarioStep.StepKeyword, 
                        scenarioStep.ScenarioBlock);
                    instances.Add(new StepInstance<TNativeSuggestionItem>(newStep, specFlowDocument, stepContext, nativeSuggestionItemFactory, 2) { ParentTemplate = this });
                }
            }
        }

        static public bool IsTemplate(Step scenarioStep)
        {
            return paramRe.Match(scenarioStep.Text).Success;
        }
    }
}