IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.SystemPreferences', 'EnableOpenClawAgents') IS NOT NULL
   AND COL_LENGTH('dbo.SystemPreferences', 'EnableAiAutomation') IS NULL
BEGIN
    EXEC sp_rename
        N'dbo.SystemPreferences.EnableOpenClawAgents',
        N'EnableAiAutomation',
        N'COLUMN';
END;

IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.SystemPreferences', 'AllowOpenClawForFreePlan') IS NOT NULL
   AND COL_LENGTH('dbo.SystemPreferences', 'AllowAiAutomationForFreePlan') IS NULL
BEGIN
    EXEC sp_rename
        N'dbo.SystemPreferences.AllowOpenClawForFreePlan',
        N'AllowAiAutomationForFreePlan',
        N'COLUMN';
END;
