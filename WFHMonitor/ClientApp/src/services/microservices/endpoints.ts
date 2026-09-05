function segment(value: string | number): string {
  return encodeURIComponent(String(value));
}

function withQuery(path: string, query: URLSearchParams): string {
  const value = query.toString();
  return value ? `${path}?${value}` : path;
}

export const apiRoutes = {
  spa: {
    bootstrap: "/api/spa/bootstrap"
  },
  auth: {
    login: "/api/Auth/Login",
    register: "/api/Auth/Register",
    logout: "/api/Auth/Logout",
    updateProfile: "/api/Auth/UpdateProfile"
  },
  notifications: {
    markAllRead: "/api/Notification/MarkAllRead",
    markRead: "/api/Notification/MarkRead"
  },
  github: {
    disconnect: "/api/GitHubAuth/Disconnect"
  },
  admin: {
    dashboard: "/api/Admin/Index",
    employees: "/api/Admin/Employees",
    addEmployee: "/api/Admin/AddEmployee",
    deleteEmployee: "/api/Admin/DeleteEmployee"
  },
  agents: {
    index: "/api/Agent/Index"
  },
  aiAccount: {
    status: "/api/AiAccount/Status",
    connect: "/api/AiAccount/Connect",
    loginStatus: (id: string) =>
      `/api/AiAccount/LoginStatus?id=${segment(id)}`,
    cancel: (id: string) =>
      `/api/AiAccount/Cancel?id=${segment(id)}`,
    logout: "/api/AiAccount/Logout"
  },
  brainstorm: {
    index: "/api/Brainstorm/Index",
    details: (id: string | number) =>
      `/api/Brainstorm/Details/${segment(id)}`,
    generate: "/api/Brainstorm/Generate",
    generateModuleImages: (id: string | number) =>
      `/api/Brainstorm/GenerateModuleImages/${segment(id)}`,
    importChatGpt: "/api/Brainstorm/ImportChatGpt",
    delete: (id: string | number) =>
      `/api/Brainstorm/Delete/${segment(id)}`
  },
  developer: {
    summary: "/api/Developer/Summary"
  },
  bugs: {
    create: "/api/Bug/Create",
    index: (status?: string | null) =>
      status
        ? `/api/Bug/Index?status=${segment(status)}`
        : "/api/Bug/Index",
    details: (id: string | number) => `/api/Bug/Details/${segment(id)}`,
    edit: (id: string | number) => `/api/Bug/Edit/${segment(id)}`,
    uploadScreenshot: "/api/Bug/UploadScreenshot",
    deleteScreenshot: "/api/Bug/DeleteScreenshot",
    uploadDocument: "/api/Bug/UploadDocument",
    deleteDocument: "/api/Bug/DeleteDocument",
    fixWithAgent: (id: string | number) =>
      `/api/Bug/FixWithAgent/${segment(id)}`,
    updateStatus: (id: string | number) =>
      `/api/Bug/UpdateStatus/${segment(id)}`,
    delete: (id: string | number) => `/api/Bug/Delete/${segment(id)}`
  },
  calendar: {
    events: (year: number, month: number) =>
      `/api/Calendar/Events?year=${segment(year)}&month=${segment(month)}`,
    create: "/api/Calendar/Create",
    feedIcs: "/api/Calendar/FeedIcs",
    eventIcs: (id: string | number) =>
      `/api/Calendar/EventIcs/${segment(id)}`,
    connectOutlook: "/api/Calendar/ConnectOutlook",
    syncOutlook: "/api/Calendar/SyncOutlook",
    disconnectOutlook: "/api/Calendar/DisconnectOutlook"
  },
  changeRequests: {
    index: "/api/ChangeRequest/Index",
    create: "/api/ChangeRequest/Create",
    details: (id: string | number) =>
      `/api/ChangeRequest/Details/${segment(id)}`,
    edit: (id: string | number) =>
      `/api/ChangeRequest/Edit/${segment(id)}`,
    features: "/api/ChangeRequest/Features",
    createFeature: (projectId?: string | null) =>
      projectId
        ? `/api/ChangeRequest/CreateFeature?projectId=${segment(projectId)}`
        : "/api/ChangeRequest/CreateFeature",
    editFeature: (id: string | number) =>
      `/api/ChangeRequest/EditFeature?featureId=${segment(id)}`,
    updateFeature: "/api/ChangeRequest/EditFeature",
    featureDetails: (id: string | number) =>
      `/api/ChangeRequest/FeatureDetails?featureId=${segment(id)}`,
    uploadImage: "/api/ChangeRequest/UploadImage",
    deleteImage: "/api/ChangeRequest/DeleteImage",
    uploadDocument: "/api/ChangeRequest/UploadDocument",
    deleteDocument: "/api/ChangeRequest/DeleteDocument",
    uploadFeatureScreenshot:
      "/api/ChangeRequest/UploadFeatureScreenshot",
    deleteFeatureScreenshot:
      "/api/ChangeRequest/DeleteFeatureScreenshot",
    pickupFeature: "/api/ChangeRequest/PickupFeature",
    toggleFeature: "/api/ChangeRequest/ToggleFeature",
    deleteFeature: "/api/ChangeRequest/DeleteFeature",
    scanFeatures: (id: string | number) =>
      `/api/ChangeRequest/ScanFeatures/${segment(id)}`,
    findBugs: (id: string | number) =>
      `/api/ChangeRequest/FindBugs/${segment(id)}`,
    analyzeHealth: (id: string | number) =>
      `/api/ChangeRequest/AnalyzeHealth/${segment(id)}`,
    delete: (id: string | number) =>
      `/api/ChangeRequest/Delete/${segment(id)}`
  },
  qa: {
    index: (query = new URLSearchParams()) =>
      withQuery("/api/Qa/Index", query),
    details: (id: string | number) => `/api/Qa/Details/${segment(id)}`,
    autoGenerate: "/api/Qa/AutoGenerate",
    scanAndGenerate: "/api/Qa/ScanAndGenerate",
    updateStatus: "/api/Qa/UpdateStatus",
    delete: "/api/Qa/Delete",
    edit: "/api/Qa/Edit",
    createTestCase: "/api/Qa/CreateTestCase"
  },
  monitor: {
    index: "/api/Monitor/Index"
  },
  onboarding: {
    welcome: (returnUrl: string) =>
      `/api/Onboarding/Welcome?returnUrl=${segment(returnUrl)}`,
    skipWelcome: "/api/Onboarding/SkipWelcome",
    setupTeam: (returnUrl?: string) =>
      returnUrl
        ? `/api/Onboarding/SetupTeam?returnUrl=${segment(returnUrl)}`
        : "/api/Onboarding/SetupTeam",
    skipSetupTeam: "/api/Onboarding/SkipSetupTeam"
  },
  payments: {
    index: "/api/Payment/Index",
    cancelPlan: "/api/Payment/CancelPlan",
    startProCheckout: "/api/Payment/StartProCheckout"
  },
  settings: {
    index: "/api/Settings/Index",
    saveModulePermissions: "/api/Settings/SaveModulePermissions",
    saveBellNotification: "/api/Settings/SaveBellNotification",
    saveProVersion: "/api/Settings/SaveProVersion"
  },
  team: {
    index: "/api/Team/Index",
    assignMember: "/api/Team/AssignMemberTeam",
    assignProject: "/api/Team/AssignProjectTeam",
    add: "/api/Team/AddTeam",
    rename: "/api/Team/RenameTeam",
    updateCeo: "/api/Team/UpdateCeo",
    delete: "/api/Team/DeleteTeam"
  }
} as const;
