IF COL_LENGTH('ProjectKickStartDesigns', 'TargetMobile') IS NULL
BEGIN
    ALTER TABLE [ProjectKickStartDesigns]
        ADD [TargetMobile] bit NOT NULL
            CONSTRAINT [DF_ProjectKickStartDesigns_TargetMobile] DEFAULT CAST(0 AS bit);
END;

IF COL_LENGTH('ProjectKickStartDesigns', 'TargetWeb') IS NULL
BEGIN
    ALTER TABLE [ProjectKickStartDesigns]
        ADD [TargetWeb] bit NOT NULL
            CONSTRAINT [DF_ProjectKickStartDesigns_TargetWeb] DEFAULT CAST(1 AS bit);
END;
