﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Guts.Business.Converters;
using Guts.Data;
using Guts.Data.Repositories;
using Guts.Domain;

namespace Guts.Business.Services
{
    public class AssignmentService : IAssignmentService
    {
        private readonly IExerciseRepository _exerciseRepository;
        private readonly IChapterService _chapterService;
        private readonly IProjectService _projectService;
        private readonly IProjectComponentRepository _projectComponentRepository;
        private readonly ITestRepository _testRepository;
        private readonly ITestResultRepository _testResultRepository;
        private readonly IAssignmentWitResultsConverter _assignmentWitResultsConverter;
        private readonly ITestRunRepository _testRunRepository;

        public AssignmentService(IExerciseRepository exerciseRepository, 
            IChapterService chapterService,
            IProjectService projectService,
            IProjectComponentRepository projectComponentRepository,
            ITestRepository testRepository,
            ITestResultRepository testResultRepository, 
            IAssignmentWitResultsConverter assignmentWitResultsConverter,
            ITestRunRepository testRunRepository)
        {
            _exerciseRepository = exerciseRepository;
            _chapterService = chapterService;
            _projectService = projectService;
            _projectComponentRepository = projectComponentRepository;
            _testRepository = testRepository;
            _testResultRepository = testResultRepository;
            _assignmentWitResultsConverter = assignmentWitResultsConverter;
            _testRunRepository = testRunRepository;
        }

        public async Task<Exercise> GetOrCreateExerciseAsync(ExerciseDto exerciseDto)
        {
            var chapter = await _chapterService.GetOrCreateChapterAsync(exerciseDto.CourseCode, exerciseDto.ChapterNumber);

            Exercise exercise;
            try
            {
                exercise = await _exerciseRepository.GetSingleAsync(chapter.Id, exerciseDto.ExerciseCode);
            }
            catch (DataNotFoundException)
            {
                exercise = new Exercise
                {
                    ChapterId = chapter.Id,
                    Code = exerciseDto.ExerciseCode
                };
                exercise = await _exerciseRepository.AddAsync(exercise);
            }

            return exercise;
        }

        public async Task<ProjectComponent> GetOrCreateProjectComponentAsync(ProjectComponentDto componentDto)
        {
            var project =
                await _projectService.GetOrCreateProjectAsync(componentDto.CourseCode, componentDto.ProjectCode);

            ProjectComponent component;
            try
            {
                component = await _projectComponentRepository.GetSingleAsync(project.Id, componentDto.ComponentCode);
            }
            catch (DataNotFoundException)
            {
                component = new ProjectComponent
                {
                    ProjectId = project.Id,
                    Code = componentDto.ComponentCode
                };
                component = await _projectComponentRepository.AddAsync(component);
            }

            return component;
        }

        public async Task LoadTestsForAssignmentAsync(Assignment assignment)
        {
            var assignmentTests = await _testRepository.FindByAssignmentId(assignment.Id);
            assignment.Tests = assignmentTests;
        }

        public async Task LoadOrCreateTestsForAssignmentAsync(Assignment assignment, IEnumerable<string> testNames)
        {
            var assignmentTests = await _testRepository.FindByAssignmentId(assignment.Id);

            foreach (var testName in testNames)
            {
                if (assignmentTests.All(test => test.TestName != testName))
                {
                    var newTest = new Test
                    {
                        AssignmentId = assignment.Id,
                        TestName = testName
                    };
                    var savedTest = await _testRepository.AddAsync(newTest);
                    assignmentTests.Add(savedTest);
                }
            }
            assignment.Tests = assignmentTests;
        }

        public async Task<AssignmentResultDto> GetResultsForUserAsync(int exerciseId, int userId, DateTime? dateUtc)
        {
            var assignmentWithLastResultsOfUser = await _testResultRepository.GetLastTestResultsOfExerciseAsync(exerciseId, userId, dateUtc);

            return _assignmentWitResultsConverter.ToAssignmentResultDto(assignmentWithLastResultsOfUser);
        }

        public async Task<ExerciseTestRunInfoDto> GetUserTestRunInfoForExercise(int exerciseId, int userId, DateTime? dateUtc)
        {
            //TODO: write unit test
            var testRunInfo = new ExerciseTestRunInfoDto();

            var testRuns = await _testRunRepository.GetUserTestRunsForExercise(exerciseId, userId, dateUtc);
            if (testRuns.Any())
            {
                var firstTestRun = testRuns.First();
                var lastTestRun = testRuns.Last();
                testRunInfo.FirstRunDateTime = firstTestRun.CreateDateTime;
                testRunInfo.LastRunDateTime = lastTestRun.CreateDateTime;
                testRunInfo.SourceCode = lastTestRun.SourceCode;
                testRunInfo.NumberOfRuns = testRuns.Count;
            }

            return testRunInfo;
        }

        public async Task<IList<ExerciseSourceDto>> GetAllSourceCodes(int exerciseId)
        {
            var testRunsWithUser = await _testRunRepository.GetLastTestRunForExerciseOfAllUsers(exerciseId);

            return testRunsWithUser.Select(testrun => new ExerciseSourceDto
            {
                Source = testrun.SourceCode,
                UserId = testrun.UserId,
                UserFullName = $"{testrun.User.FirstName} {testrun.User.LastName}".Trim()
            }).OrderBy(dto => dto.UserFullName).ToList();
        }
    }
}