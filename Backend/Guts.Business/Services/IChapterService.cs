﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Guts.Domain;

namespace Guts.Business.Services
{
    public interface IChapterService
    {
        Task<Chapter> GetOrCreateChapterAsync(string courseCode, int chapterNumber);
        Task<Chapter> LoadChapterAsync(int courseId, int chapterNumber);
        Task<Chapter> LoadChapterWithTestsAsync(int courseId, int chapterNumber);
        Task<IList<AssignmentResultDto>> GetResultsForUserAsync(int chapterId, int userId, DateTime? dateUtc);
        Task<IList<AssignmentStatisticsDto>> GetChapterStatisticsAsync(int chapterId, DateTime? dateUtc);
        Task<IList<Chapter>> GetChaptersOfCourseAsync(int courseId);
       
    }
}