﻿using System;
using System.Collections.Generic;
using System.Linq;
using Guts.Api.Models;
using Guts.Api.Models.Converters;
using Guts.Api.Tests.Builders;
using Guts.Business.Tests.Builders;
using Guts.Common.Extensions;
using Guts.Domain;
using NUnit.Framework;

namespace Guts.Api.Tests.Models.Converters
{

    [TestFixture]
    internal class TestRunConverterTests
    {
        private TestRunConverter _converter;
        private readonly Random _random;
        private int _userId;
        private ExerciseCreateTestRunModel _exerciseCreateTestRunModel;

        public TestRunConverterTests()
        {
            _random = new Random();
        }

        [SetUp]
        public void Setup()
        {
            _converter = new TestRunConverter();
            _userId = _random.NextPositive();
            _exerciseCreateTestRunModel = new ExerciseCreateTestRunModelBuilder().Build();
        }

        [Test]
        public void From_ShouldCorrectlyConvertValidTestRunModel()
        {
            //Arrange
            var numberOfTests = 2;

            var exercise = new ExerciseBuilder().WithRandomTests(numberOfTests).Build();
            _exerciseCreateTestRunModel = new ExerciseCreateTestRunModelBuilder()
                .WithSourceCode()
                .WithRandomTestResultModelsFor(exercise.Tests)
                .Build();

            //Act
            var testRun = _converter.From(_exerciseCreateTestRunModel.Results, _exerciseCreateTestRunModel.SourceCode, _userId, exercise);

            //Assert
            Assert.That(testRun, Is.Not.Null);
            Assert.That(testRun.UserId, Is.EqualTo(_userId));
            Assert.That(testRun.AssignmentId, Is.EqualTo(exercise.Id));
            Assert.That(testRun.SourceCode, Is.EqualTo(_exerciseCreateTestRunModel.SourceCode));
            Assert.That(testRun.CreateDateTime, Is.EqualTo(DateTime.Now).Within(5).Seconds);
            Assert.That(testRun.TestResults.Count, Is.EqualTo(numberOfTests));          

            for (int i = 0; i < _exerciseCreateTestRunModel.Results.Count(); i++)
            {
                var testResult = testRun.TestResults.ElementAt(i);
                var testResultModel = _exerciseCreateTestRunModel.Results.ElementAt(i);
                var test = exercise.Tests.ElementAt(i);

                Assert.That(testResult.TestId, Is.EqualTo(test.Id));
                Assert.That(testResult.Passed, Is.EqualTo(testResultModel.Passed));
                Assert.That(testResult.Message, Is.EqualTo(testResultModel.Message));
                Assert.That(testResult.UserId, Is.EqualTo(_userId));
                Assert.That(testResult.CreateDateTime, Is.EqualTo(DateTime.Now).Within(5).Seconds);
            }    
        }

        [Test]
        public void From_ShouldThrowExceptionIfExerciseIsNotProvided()
        {
            //Act + Assert
            Assert.That(() => _converter.From(_exerciseCreateTestRunModel.Results, null, _userId, null), Throws.ArgumentNullException);
        }

        [Test]
        public void From_ShouldThrowExceptionIfThereAreResultsForNonExisingTests()
        {
            //Assert
            var exercise = new ExerciseBuilder().Build();
            _exerciseCreateTestRunModel = new ExerciseCreateTestRunModelBuilder().WithRandomTestResultModels(1).Build();

            //Act + Assert
            Assert.That(() => _converter.From(_exerciseCreateTestRunModel.Results, null, _userId, exercise), Throws.ArgumentException);
        }

        [Test]
        public void From_ShouldConvertRunWithEmptyListOfTestResultsIfModelDoesNotContainTestResults()
        {
            //Arrange
            var exercise = new ExerciseBuilder().Build();
            _exerciseCreateTestRunModel = new ExerciseCreateTestRunModelBuilder().WithTestResultModels(null).Build();

            //Act
            var testRun = _converter.From(_exerciseCreateTestRunModel.Results, null, _userId, exercise);

            //Assert
            Assert.That(testRun, Is.Not.Null);
            Assert.That(testRun.TestResults, Is.Not.Null);
            Assert.That(testRun.TestResults.Count, Is.Zero);
        }

        [Test]
        public void ToTestRunModel_ShouldCorrectlyConvertValidTestRun()
        {
            //Arrange
            var testRun = new TestRun
            {
                Id = _random.NextPositive(),
                CreateDateTime = DateTime.Now,
                AssignmentId = _random.NextPositive(),
                TestResults = new List<TestResult>
                {
                    new TestResult
                    {
                        Id = _random.NextPositive(),
                        Passed = true,
                        TestId = _random.NextPositive()
                    }
                }
            }; 

            //Act
            var model = _converter.ToTestRunModel(testRun);

            //Assert
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Id, Is.EqualTo(testRun.Id));
            Assert.That(model.ExerciseId, Is.EqualTo(testRun.AssignmentId));
            Assert.That(model.TestResults, Is.Not.Null);
            Assert.That(model.TestResults.Count, Is.EqualTo(testRun.TestResults.Count));
            var firstResult = model.TestResults.First();
            Assert.That(firstResult.Id, Is.EqualTo(testRun.TestResults.First().Id));
            Assert.That(firstResult.Passed, Is.EqualTo(testRun.TestResults.First().Passed));
        }

    }
}
