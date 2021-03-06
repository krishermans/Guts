using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Guts.Common.Extensions;
using Guts.Domain;

namespace Guts.Business.Tests.Builders
{
    public class ChapterBuilder
    {
        private readonly Random _random;
        private readonly Chapter _chapter;

        public ChapterBuilder()
        {
            _random = new Random();
            _chapter = new Chapter
            {
                Id = 0,
                Number = _random.NextPositive(),
                CourseId = _random.NextPositive(),
                PeriodId = _random.NextPositive(),
                Exercises = new Collection<Exercise>()
            };
        }

        public ChapterBuilder WithId()
        {
            _chapter.Id = _random.NextPositive();
            return this;
        }

        public ChapterBuilder WithCourseId(int courseId)
        {
            _chapter.CourseId = courseId;
            return this;
        }

        public ChapterBuilder WithCourse()
        {
            _chapter.Course = new CourseBuilder().WithId().Build();
            _chapter.CourseId = _chapter.Course.Id;
            return this;
        }

        public ChapterBuilder WithCourse(string courseCode)
        {
            _chapter.Course = new CourseBuilder().WithId().WithCourseCode(courseCode).Build();
            _chapter.CourseId = _chapter.Course.Id;
            return this;
        }

        public ChapterBuilder WithoutCourseLoaded()
        {
            _chapter.Course = null;
            return this;
        }

        public ChapterBuilder WithPeriod(Period period)
        {
            _chapter.Period = period;
            _chapter.PeriodId = period.Id;
            return this;
        }

        public ChapterBuilder WithExercises(int numberOfExercises, int numberOfTestsPerExercise)
        {
            _chapter.Exercises = new List<Exercise>();

            for (int i = 0; i < numberOfExercises; i++)
            {
                var exercise = new ExerciseBuilder().WithRandomTests(numberOfTestsPerExercise).Build();

                _chapter.Exercises.Add(exercise);
            }

            return this;
        }

        public ChapterBuilder WithNumber(int number)
        {
            _chapter.Number = number;
            return this;
        }

        public Chapter Build()
        {
            return _chapter;
        }
    }
}