﻿using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Promact.Oauth.Server.Constants;
using Promact.Oauth.Server.Data_Repository;
using Promact.Oauth.Server.ExceptionHandler;
using Promact.Oauth.Server.Models;
using Promact.Oauth.Server.Models.ApplicationClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Promact.Oauth.Server.Data;


namespace Promact.Oauth.Server.Repository.ProjectsRepository
{
    public class ProjectRepository : IProjectRepository
    {
        #region "Private Variable(s)"
        private readonly IDataRepository<Project, PromactOauthDbContext> _projectDataRepository;
        private readonly IDataRepository<ProjectUser, PromactOauthDbContext> _projectUserDataRepository;
        private readonly IDataRepository<ApplicationUser, PromactOauthDbContext> _userDataRepository;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringConstant _stringConstant;
        private readonly IMapper _mapperContext;
        #endregion

        #region "Constructor"
        public ProjectRepository(IDataRepository<Project, PromactOauthDbContext> projectDataRepository, IDataRepository<ProjectUser, PromactOauthDbContext> projectUserDataRepository, IDataRepository<ApplicationUser, PromactOauthDbContext> userDataRepository, UserManager<ApplicationUser> userManager,
            IMapper mapperContext, IStringConstant stringConstant, ILogger<ProjectRepository> logger)
        {
            _projectDataRepository = projectDataRepository;
            _projectUserDataRepository = projectUserDataRepository;
            _userDataRepository = userDataRepository;
            _mapperContext = mapperContext;
            _userManager = userManager;
            _stringConstant = stringConstant;
        }
        #endregion

        #region Public Method(s)

        /// <summary>
        /// This method getting the list of all projects
        /// </summary>
        /// <returns>list of projects</returns>
        public async Task<IEnumerable<ProjectAc>> GetAllProjectsAsync()
        {
            var projects = await _projectDataRepository.GetAll().ToListAsync();
            var projectAcList = new List<ProjectAc>();
            foreach (var project in projects)
            {
                var userAc = new UserAc();
                if (!string.IsNullOrEmpty(project.TeamLeaderId))
                {
                    var user = await _userDataRepository.FirstAsync(x => x.Id.Equals(project.TeamLeaderId));
                    userAc = _mapperContext.Map<ApplicationUser, UserAc>(user);
                }
                else
                {
                    userAc.FirstName = _stringConstant.TeamLeaderNotAssign;
                    userAc.LastName = _stringConstant.TeamLeaderNotAssign;
                    userAc.Email = _stringConstant.TeamLeaderNotAssign;
                }
                var projectAc = _mapperContext.Map<Project, ProjectAc>(project);
                projectAc.TeamLeader = userAc;
                projectAc.CreatedBy = (await _userDataRepository.FirstAsync(x => x.Id == project.CreatedBy)).FirstName;
                projectAc.CreatedDate = project.CreatedDateTime;
                projectAc.UpdatedBy = (await _userDataRepository.FirstOrDefaultAsync(x => x.Id == project.UpdatedBy))?.FirstName;
                projectAc.UpdatedDate = project.UpdatedDateTime;
                projectAcList.Add(projectAc);
            }
            return projectAcList;
        }

        /// <summary>
        /// This method is used to add new project
        /// </summary>
        /// <param name="newProject">project that need to be added</param>
        /// <param name="createdBy">passed id of user who has create this project</param>
        /// <returns>project id of newly created project</returns>
        public async Task<int> AddProjectAsync(ProjectAc newProject, string createdBy)
        {
            var project = _mapperContext.Map<ProjectAc, Project>(newProject);
            project.CreatedDateTime = DateTime.UtcNow;
            project.CreatedBy = createdBy;
            project.ApplicationUsers = null;
            _projectDataRepository.AddAsync(project);
            await _projectDataRepository.SaveChangesAsync();
            return project.Id;
        }

        /// <summary>
        ///This method to add user id and project id in userproject table
        /// </summary>
        /// <param name="newProjectUser">projectuser object</param>
        public async Task AddUserProjectAsync(ProjectUser newProjectUser)
        {
            _projectUserDataRepository.AddAsync(newProjectUser);
            await _projectUserDataRepository.SaveChangesAsync();
        }

        /// <summary>
        /// This method to return the project details of the given id 
        /// </summary>
        /// <param name="id">project id</param>
        /// <returns>project infromation </returns>
        public async Task<ProjectAc> GetProjectByIdAsync(int id)
        {
            var project = await _projectDataRepository.FirstOrDefaultAsync(x => x.Id == id);
            if (project != null)
            {
                var userIdList = await _projectUserDataRepository.Fetch(y => y.ProjectId == project.Id).Select(x => x.UserId).ToListAsync();
                var userList = await _userDataRepository.Fetch(y => userIdList.Contains(y.Id)).OrderBy(y => y.FirstName).ToListAsync();
                List<UserAc> userAcList = _mapperContext.Map<List<ApplicationUser>, List<UserAc>>(userList);
                var projectAc = _mapperContext.Map<Project, ProjectAc>(project);
                if (!string.IsNullOrEmpty(project.TeamLeaderId))
                {
                    var teamLeader = await _userDataRepository.FirstAsync(x => x.Id.Equals(project.TeamLeaderId));
                    projectAc.TeamLeader = new UserAc { FirstName = teamLeader.FirstName, LastName = teamLeader.LastName, Email = teamLeader.Email };
                }
                else { projectAc.TeamLeader = null; }
                projectAc.ApplicationUsers = userAcList;
                return projectAc;
            }
            else
            {
                throw new ProjectNotFound();
            }
        }

        /// <summary>
        /// This method to update project information 
        /// </summary>
        /// <param name="id">project id</param> 
        /// <param name="editProject">updated project object</param> 
        /// <param name="updatedBy">passed id of user who has update this project</param>
        /// <returns>project id</returns>
        public async Task<int> EditProjectAsync(int id, ProjectAc editProject, string updatedBy)
        {
            var project = await _projectDataRepository.FirstOrDefaultAsync(x => x.Id == id);
            if (project != null)
            {
                project.IsActive = editProject.IsActive;
                project.Name = editProject.Name;
                project.TeamLeaderId = editProject.TeamLeaderId;
                project.UpdatedDateTime = DateTime.UtcNow;
                project.UpdatedBy = updatedBy;
                _projectDataRepository.UpdateAsync(project);
                await _projectDataRepository.SaveChangesAsync();

                //Delete old users from project user table
                _projectUserDataRepository.Delete(x => x.ProjectId == id);
                await _projectUserDataRepository.SaveChangesAsync();

                foreach (var user in editProject.ApplicationUsers)
                {
                    _projectUserDataRepository.AddAsync(new ProjectUser
                    {
                        ProjectId = project.Id,
                        UpdatedDateTime = DateTime.UtcNow,
                        UpdatedBy = updatedBy,
                        CreatedBy = project.CreatedBy,
                        CreatedDateTime = project.CreatedDateTime,
                        UserId = user.Id,
                    });
                }

                await _projectDataRepository.SaveChangesAsync();
                return editProject.Id;
            }
            else
            {
                throw new ProjectNotFound();
            }
        }

        /// <summary>
        /// this method to check Project is already exists or not 
        /// </summary>
        /// <param name="project">projectAc object</param> 
        /// <returns>projectAc object</returns>
        public async Task<ProjectAc> CheckDuplicateProjectAsync(ProjectAc project)
        {
            string projectName;
            if (project.Id == 0) // for new project
            {
                projectName = (await _projectDataRepository.FirstOrDefaultAsync(x => x.Name == project.Name))?.Name;
            }
            else // for edit project
            {
                projectName = (await _projectDataRepository.FirstOrDefaultAsync(x => x.Id != project.Id && x.Name == project.Name))?.Name;
            }
            //if project name already exists then return project name as null
            if (!string.IsNullOrEmpty(projectName))
            { project.Name = null; return project; }
            return project;
        }

        /// <summary>
        /// This method to return all project for specific user
        /// </summary>
        /// <param name="userId">passed login user id</param>
        /// <returns>project information</returns>
        public async Task<IEnumerable<ProjectAc>> GetAllProjectForUserAsync(string userId)
        {
            List<ProjectAc> projectAcList = new List<ProjectAc>();
            //list of project where specific user are team leader.
            var projects = await _projectDataRepository.Fetch(x => x.TeamLeaderId == userId).ToListAsync();
            if (projects != null)
            {
                foreach (var project in projects)
                {
                    var teamLeader = await _userManager.FindByIdAsync(project.TeamLeaderId);
                    var projectMapper = _mapperContext.Map<ApplicationUser, UserAc>(teamLeader);
                    ProjectAc projectAc = _mapperContext.Map<Project, ProjectAc>(project);
                    projectAc.TeamLeader = projectMapper;
                    projectAcList.Add(projectAc);
                }
            }
            //list of project where user are as team member.
            var projectUser = await _projectUserDataRepository.Fetch(x => x.UserId == userId).ToListAsync();
            foreach (var project in projectUser)
            {
                var projectDetails = await _projectDataRepository.FirstAsync(x => x.Id == project.ProjectId);
                var teamLeader = await _userManager.FindByIdAsync(projectDetails.TeamLeaderId);
                var projectMapper = _mapperContext.Map<ApplicationUser, UserAc>(teamLeader);
                ProjectAc projectAc = _mapperContext.Map<Project, ProjectAc>(projectDetails);
                projectAc.TeamLeader = projectMapper;
                projectAcList.Add(projectAc);
            }
            return projectAcList;
        }

        /// <summary>
        /// Method to return list of projects along with the users and teamleader in a project - GA
        /// </summary>
        /// <returns>list of projects along with users</returns>
        public async Task<IList<ProjectAc>> GetProjectsWithUsersAsync()
        {
            List<ProjectAc> projectList = new List<ProjectAc>();
            List<Project> projects = await _projectDataRepository.Fetch(x => !string.IsNullOrEmpty(x.TeamLeaderId)).ToListAsync();

            foreach (var project in projects)
            {
                ProjectAc projectAc = await AssignTeamMembers(project);
                projectList.Add(projectAc);
            }
            return projectList;
        }

        /// <summary>
        /// Method to return project details by using projectId - GA
        /// </summary>
        /// <param name="projectId">passed project Id</param>
        /// <returns>project details along with users</returns>
        public async Task<ProjectAc> GetProjectDetailsAsync(int projectId)
        {
            Project project = await _projectDataRepository.FirstOrDefaultAsync(x => x.Id == projectId);
            ProjectAc projectAc = await AssignTeamMembers(project);
            return projectAc;
        }


        /// <summary>
        /// Method to return active project details of the given projectId - JJ
        /// </summary>
        /// <param name="projectId">project Id</param>
        /// <returns>object of ProjectAc</returns>
        public async Task<ProjectAc> GetProjectByProjectIdAsync(int projectId)
        {
            Project project = await _projectDataRepository.FirstOrDefaultAsync(x => x.Id == projectId);
            ProjectAc projectAc = new ProjectAc();
            if (!string.IsNullOrEmpty(project?.TeamLeaderId))
            {
                ApplicationUser teamLeader = await _userManager.FindByIdAsync(project.TeamLeaderId);
                if (teamLeader != null && teamLeader.IsActive)
                {
                    projectAc = _mapperContext.Map<Project, ProjectAc>(project);
                    //fetches the ids of users of the project
                    List<string> userIdList = await _projectUserDataRepository.Fetch(x => x.ProjectId == project.Id).Select(y => y.UserId).ToListAsync();
                    //fetches the application users of the above obtained ids.
                    List<ApplicationUser> userList = await _userDataRepository.Fetch(y => userIdList.Contains(y.Id)).ToListAsync();
                    projectAc.ApplicationUsers = _mapperContext.Map<List<ApplicationUser>, List<UserAc>>(userList);
                }
            }
            return projectAc;
        }


        #endregion

        #region Private Method(s)

        /// <summary>
        /// Method to get list of project for an user
        /// </summary>
        /// <param name="userId">user's user Id</param>
        /// <returns>list of project</returns>
        public async Task<List<ProjectAc>> GetListOfProjectsEnrollmentOfUserByUserIdAsync(string userId)
        {
            List<ProjectAc> projects = new List<ProjectAc>();
            var projectIds = (await _projectUserDataRepository.FetchAsync(x => x.UserId == userId)).Select(x => x.ProjectId).ToList();
            projectIds.AddRange((await _projectDataRepository.FetchAsync(x => x.TeamLeaderId == userId)).Select(x => x.Id));
            foreach (var projectId in projectIds)
            {
                var project = await _projectDataRepository.FirstAsync(x => x.Id == projectId);
                var projectAC = _mapperContext.Map<Project, ProjectAc>(project);
                if (!string.IsNullOrEmpty(projectAC.TeamLeaderId) && projectAC.TeamLeaderId == userId)
                {
                    ApplicationUser appUser = await _userDataRepository.FirstOrDefaultAsync(x => x.Id == userId);
                    projectAC.TeamLeader = _mapperContext.Map<ApplicationUser, UserAc>(appUser);
                }
                projects.Add(projectAC);
            }
            return projects;
        }

        /// <summary>
        /// Method to get list of team member by project Id
        /// </summary>
        /// <param name="projectId">project Id</param>
        /// <returns>list of team members</returns>
        public async Task<List<UserAc>> GetListOfTeamMemberByProjectIdAsync(int projectId)
        {
            List<UserAc> teamMembers = new List<UserAc>();
            var teamMemberIds = (await _projectUserDataRepository.FetchAsync(x => x.ProjectId == projectId)).Select(x => x.UserId).ToList();
            foreach (var teamMemberId in teamMemberIds)
            {
                teamMembers.Add(_mapperContext.Map<ApplicationUser, UserAc>(await _userManager.FindByIdAsync(teamMemberId)));
            }
            return teamMembers;
        }

        /// <summary>
        /// Method to assign teamleader and users in a project -GA
        /// </summary>
        /// <param name="project"></param>
        /// <returns>members in a project</returns>
        private async Task<ProjectAc> AssignTeamMembers(Project project)
        {
            //getting the teamleader details
            ApplicationUser applicationUser = await _userDataRepository.FirstAsync(x => x.Id == project.TeamLeaderId);
            UserAc teamLeader = _mapperContext.Map<ApplicationUser, UserAc>(applicationUser);
            teamLeader.Role = _stringConstant.TeamLeader;
            //mapping from project to projectAc
            ProjectAc projectAc = _mapperContext.Map<Project, ProjectAc>(project);
            projectAc.CreatedDate = project.CreatedDateTime;
            projectAc.TeamLeader = teamLeader;
            //getting users in the project
            List<ProjectUser> projectUsers = await _projectUserDataRepository.Fetch(x => x.ProjectId == project.Id).ToListAsync();
            foreach (var projectUser in projectUsers)
            {
                ApplicationUser user = await _userDataRepository.FirstAsync(x => x.Id == projectUser.UserId);
                UserAc userAc = _mapperContext.Map<ApplicationUser, UserAc>(user);
                userAc.Role = _stringConstant.Employee;
                projectAc.ApplicationUsers.Add(userAc);
            }
            return projectAc;
        }

        #endregion
    }
}