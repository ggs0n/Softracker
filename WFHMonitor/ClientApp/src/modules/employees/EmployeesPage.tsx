import { useState, type FormEvent } from "react";
import {
  DataTable,
  PageHeader,
  StatusChip
} from "../../components/DataDisplay";
import { PageBoundary } from "../../components/PageBoundary";
import { Notice } from "../../components/PageStates";
import { ServerAction } from "../../components/ServerAction";
import { usePage } from "../../hooks/usePage";
import {
  ApiError,
  apiRoutes,
  postForm
} from "../../services/microservices";
import { useSession } from "../../state/SessionContext";
import type {
  ApplicationUser,
  UnknownRecord
} from "../../types/api";

interface EmployeesModel {
  employees: Array<{ employee: ApplicationUser; role: string }>;
}

export function EmployeesPage() {
  const page = usePage<EmployeesModel>(apiRoutes.admin.employees);
  const [message, setMessage] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [newEmployee, setNewEmployee] = useState<UnknownRecord>({
    fullName: "",
    email: "",
    role: "Employee",
    password: "",
    confirmPassword: ""
  });
  const { session } = useSession();
  const [error, setError] = useState<string | null>(null);

  async function addEmployee(event: FormEvent) {
    event.preventDefault();
    if (!session) return;
    try {
      setError(null);
      const response = await postForm(
        apiRoutes.admin.addEmployee,
        { newEmployee },
        session.antiForgeryToken
      );
      setMessage(response.success ?? "Employee added.");
      setShowForm(false);
      void page.reload();
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.message
          : "Employee could not be added."
      );
    }
  }

  return (
    <PageBoundary {...page}>
      <>
        {message && (
          <Notice onDismiss={() => setMessage(null)}>{message}</Notice>
        )}
        <PageHeader
          eyebrow="Organization"
          title="Employees"
          description="Manage workspace access and delivery roles."
          actions={
            <button
              className="button button-primary"
              type="button"
              onClick={() => setShowForm((open) => !open)}
            >
              <i className="bi bi-person-plus" /> Add employee
            </button>
          }
        />
        {showForm && (
          <form
            className="surface compact-form"
            onSubmit={(event) => void addEmployee(event)}
          >
            {error && <Notice kind="error">{error}</Notice>}
            <label>
              <span>Full name</span>
              <input
                value={String(newEmployee.fullName)}
                onChange={(event) =>
                  setNewEmployee((current) => ({
                    ...current,
                    fullName: event.target.value
                  }))}
              />
            </label>
            <label>
              <span>Email</span>
              <input
                type="email"
                required
                value={String(newEmployee.email)}
                onChange={(event) =>
                  setNewEmployee((current) => ({
                    ...current,
                    email: event.target.value
                  }))}
              />
            </label>
            <label>
              <span>Role</span>
              <select
                value={String(newEmployee.role)}
                onChange={(event) =>
                  setNewEmployee((current) => ({
                    ...current,
                    role: event.target.value
                  }))}
              >
                <option>Employee</option>
                <option>Developer</option>
                <option>Tester</option>
                <option>Agent</option>
              </select>
            </label>
            <label>
              <span>Password</span>
              <input
                type="password"
                required
                value={String(newEmployee.password)}
                onChange={(event) =>
                  setNewEmployee((current) => ({
                    ...current,
                    password: event.target.value,
                    confirmPassword: event.target.value
                  }))}
              />
            </label>
            <button className="button button-primary" type="submit">
              Add employee
            </button>
          </form>
        )}
        <section className="surface">
          <DataTable
            rows={page.data?.employees ?? []}
            rowKey={(item) => item.employee.id}
            columns={[
              {
                key: "employee",
                label: "Name",
                render: (item) =>
                  item.employee.fullName || item.employee.email
              },
              {
                key: "email",
                label: "Email",
                render: (item) => item.employee.email
              },
              {
                key: "role",
                label: "Role",
                render: (item) => <StatusChip value={item.role} />
              },
              {
                key: "team",
                label: "Team",
                render: (item) =>
                  item.employee.orgTeam?.name ?? "Unassigned"
              },
              {
                key: "isActive",
                label: "Status",
                render: (item) => (
                  <StatusChip
                    value={item.employee.isActive ? "Active" : "Inactive"}
                  />
                )
              },
              {
                key: "actions",
                label: "",
                render: (item) => (
                  <ServerAction
                    endpoint={apiRoutes.admin.deleteEmployee}
                    values={{ userId: item.employee.id }}
                    className="text-button danger"
                    confirmMessage={`Remove ${item.employee.fullName || item.employee.email} from this workspace?`}
                    onComplete={(response) => {
                      setMessage(response.success ?? "Employee removed.");
                      void page.reload();
                    }}
                  >
                    Remove
                  </ServerAction>
                )
              }
            ]}
          />
        </section>
      </>
    </PageBoundary>
  );
}
