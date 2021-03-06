﻿using System;
using Guts.Client.Shared.Models;
using Guts.Client.Shared.Utility;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Guts.Client.Classic
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ExerciseTestFixtureAttribute : MonitoredTestFixtureBaseAttribute
    {
        private readonly int _chapter;
        private readonly string _exerciseCode;

        public ExerciseTestFixtureAttribute(string courseCode, int chapter, string exerciseCode) : base(courseCode)
        {
            _chapter = chapter;
            _exerciseCode = exerciseCode;
        }

        public ExerciseTestFixtureAttribute(string courseCode, int chapter, string exerciseCode, string sourceCodeRelativeFilePaths) : this(courseCode, chapter, exerciseCode)
        {
            _sourceCodeRelativeFilePaths = sourceCodeRelativeFilePaths;
        }

        public override void BeforeTest(ITest test)
        {
            TestRunResultAccumulator.Instance.Clear();
            TestContext.Progress.WriteLine($"Starting test run. Chapter {_chapter}, exercise {_exerciseCode}");
        }

        public override void AfterTest(ITest test)
        {
            TestContext.Progress.WriteLine("Test run completed.");

            try
            {
                var results = TestRunResultAccumulator.Instance.TestResults;

                var exercise = new Exercise
                {
                    CourseCode = _courseCode,
                    ChapterNumber = _chapter,
                    ExerciseCode = _exerciseCode,
                };

                var testRun = new ExerciseTestRun
                {
                    Exercise = exercise,
                    Results = results,
                };

                if (!string.IsNullOrEmpty(_sourceCodeRelativeFilePaths))
                {
                    TestContext.Progress.WriteLine($"Reading source code files: {_sourceCodeRelativeFilePaths}");
                    testRun.SourceCode = SourceCodeRetriever.ReadSourceCodeFiles(_sourceCodeRelativeFilePaths);
                }

                SendTestResults(testRun);
            }
            catch (Exception ex)
            {
                TestContext.Error.WriteLine("Something went wrong while sending the test results.");
                TestContext.Error.WriteLine($"Exception: {ex}");
            }
        }
    }
}
