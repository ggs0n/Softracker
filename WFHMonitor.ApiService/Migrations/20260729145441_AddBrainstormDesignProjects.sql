BEGIN TRANSACTION;
GO

CREATE TABLE [BrainstormDesignProjects] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(180) NOT NULL,
    [Summary] nvarchar(max) NOT NULL,
    [Technology] nvarchar(max) NOT NULL,
    [CloudHostingTarget] nvarchar(120) NULL,
    [UserCount] nvarchar(100) NOT NULL,
    [Features] nvarchar(max) NOT NULL,
    [BlueprintJson] nvarchar(max) NOT NULL,
    [SourceMode] nvarchar(40) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedById] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_BrainstormDesignProjects] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BrainstormDesignProjects_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_BrainstormDesignProjects_CreatedById] ON [BrainstormDesignProjects] ([CreatedById]);
GO

CREATE INDEX [IX_BrainstormDesignProjects_CreatedById_CreatedAt] ON [BrainstormDesignProjects] ([CreatedById], [CreatedAt]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260729145441_AddBrainstormDesignProjects', N'8.0.29');
GO

COMMIT;
GO

