import { useState } from "react";
import { PageHeader, StatusChip } from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { Notice } from "../../components/PageStates";
import { Link } from "../../state/RouterContext";
import { usePage } from "../../hooks/usePage";
import { apiRoutes } from "../../services/microservices";
import type { ApiEnvelope, UnknownRecord } from "../../types/api";
import { InlineMutationForm } from "./components/InlineMutationForm";

interface TeamMember {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  teamId?: number | null;
  isOnline: boolean;
}

interface TeamProject {
  projectId: number;
  crNumber: string;
  title: string;
  stage: string;
  status: string;
}

interface TeamModel {
  ceoName: string;
  ceoEmail: string;
  teams: Array<{
    teamId: number;
    teamName: string;
    members: TeamMember[];
    projects: TeamProject[];
  }>;
  unassignedMembers: TeamMember[];
  unassignedProjects: TeamProject[];
  assignmentForm: {
    memberOptions: Array<{ value: string; text: string }>;
    teamOptions: Array<{ value: string; text: string }>;
  };
  projectAssignmentForm: {
    projectOptions: Array<{ value: string; text: string }>;
    teamOptions: Array<{ value: string; text: string }>;
  };
  teamRenameForm: {
    teamOptions: Array<{ value: string; text: string }>;
  };
  teamDeleteForm: {
    teamOptions: Array<{ value: string; text: string }>;
  };
  ceoForm: {
    ceoOptions: Array<{ value: string; text: string }>;
  };
}

export function TeamPage() {
  const page = usePage<TeamModel>(apiRoutes.team.index);
  const [message, setMessage] = useState<string | null>(null);
  const [memberValues, setMemberValues] = useState<UnknownRecord>({});
  const [projectValues, setProjectValues] = useState<UnknownRecord>({});
  const [teamValues, setTeamValues] = useState<UnknownRecord>({});
  const [renameValues, setRenameValues] = useState<UnknownRecord>({});
  const [deleteValues, setDeleteValues] = useState<UnknownRecord>({});
  const [ceoValues, setCeoValues] = useState<UnknownRecord>({});
  const complete = (response: ApiEnvelope<unknown>) => {
    setMessage(response.success ?? "Team settings updated.");
    void page.reload();
  };

  return (
    <PageBoundary {...page}>
      {page.data && (
        <>
          {message && (
            <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
          )}
          <PageHeader
            eyebrow="Organization"
            title="Team structure"
            description={`${page.data.ceoName} · ${page.data.ceoEmail}`}
          />
          <section className="team-grid">
            {page.data.teams.map((team) => (
              <article className="surface team-card" key={team.teamId}>
                <header>
                  <div>
                    <span className="eyebrow">Delivery team</span>
                    <h2>{team.teamName}</h2>
                  </div>
                  <span>
                    {team.members.length} people · {team.projects.length} projects
                  </span>
                </header>
                <div className="team-members">
                  {team.members.map((member) => (
                    <span key={member.userId}>
                      <i className={member.isOnline ? "online" : ""}>
                        {member.fullName.slice(0, 2).toUpperCase()}
                      </i>
                      <span>
                        <strong>{member.fullName}</strong>
                        <small>{member.role}</small>
                      </span>
                    </span>
                  ))}
                </div>
                <ul className="team-projects">
                  {team.projects.map((project) => (
                    <li key={project.projectId}>
                      <Link to={`/projects/${project.projectId}`}>
                        <small>{project.crNumber}</small>
                        {project.title}
                      </Link>
                      <StatusChip value={project.stage} />
                    </li>
                  ))}
                </ul>
              </article>
            ))}
          </section>
          <section className="surface management-forms">
            <div className="section-heading">
              <div>
                <span className="eyebrow">Manage</span>
                <h2>Organization settings</h2>
              </div>
            </div>
            <div className="management-grid">
              <InlineMutationForm
                endpoint={apiRoutes.team.assignMember}
                values={memberValues}
                setValues={setMemberValues}
                prefix="assignmentForm"
                fields={[
                  {
                    name: "memberId",
                    label: "Member",
                    options: page.data.assignmentForm.memberOptions
                  },
                  {
                    name: "teamId",
                    label: "Team",
                    options: page.data.assignmentForm.teamOptions
                  }
                ]}
                submitLabel="Assign member"
                onComplete={complete}
              />
              <InlineMutationForm
                endpoint={apiRoutes.team.assignProject}
                values={projectValues}
                setValues={setProjectValues}
                prefix="projectAssignmentForm"
                fields={[
                  {
                    name: "projectId",
                    label: "Project",
                    options: page.data.projectAssignmentForm.projectOptions
                  },
                  {
                    name: "teamId",
                    label: "Team",
                    options: page.data.projectAssignmentForm.teamOptions
                  }
                ]}
                submitLabel="Assign project"
                onComplete={complete}
              />
              <InlineMutationForm
                endpoint={apiRoutes.team.add}
                values={teamValues}
                setValues={setTeamValues}
                prefix="teamCreateForm"
                fields={[
                  {
                    name: "name",
                    label: "Team name",
                    placeholder: "e.g. Payments"
                  }
                ]}
                submitLabel="Create team"
                onComplete={complete}
              />
              <InlineMutationForm
                endpoint={apiRoutes.team.rename}
                values={renameValues}
                setValues={setRenameValues}
                prefix="teamRenameForm"
                fields={[
                  {
                    name: "teamId",
                    label: "Team",
                    options: page.data.teamRenameForm.teamOptions
                  },
                  {
                    name: "name",
                    label: "New name",
                    placeholder: "Updated team name"
                  }
                ]}
                submitLabel="Rename team"
                onComplete={complete}
              />
              <InlineMutationForm
                endpoint={apiRoutes.team.updateCeo}
                values={ceoValues}
                setValues={setCeoValues}
                prefix="ceoForm"
                fields={[
                  {
                    name: "ceoUserId",
                    label: "Workspace CEO",
                    options: page.data.ceoForm.ceoOptions
                  }
                ]}
                submitLabel="Update CEO"
                onComplete={complete}
              />
              <InlineMutationForm
                endpoint={apiRoutes.team.delete}
                values={deleteValues}
                setValues={setDeleteValues}
                prefix="teamDeleteForm"
                fields={[
                  {
                    name: "teamId",
                    label: "Team to delete",
                    options: page.data.teamDeleteForm.teamOptions
                  }
                ]}
                submitLabel="Delete team"
                onComplete={complete}
                confirmMessage="Delete this team? Members and projects will become unassigned."
              />
            </div>
          </section>
        </>
      )}
    </PageBoundary>
  );
}
