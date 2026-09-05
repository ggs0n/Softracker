import { useEffect, useMemo, useState } from "react";
import { useSession } from "../../state/SessionContext";
import {
  cancelAiLogin,
  connectAiAccount,
  deleteDesign,
  generateDesign,
  getAiAccountStatus,
  getDesign,
  getAiLoginStatus,
  importChatGptResult,
  listDesigns,
  logoutAiAccount
} from "./api";
import { BlueprintWorkspace } from "./components/BlueprintWorkspace";
import { BrainstormInputPanel } from "./components/BrainstormInputPanel";
import { SavedDesignsPanel } from "./components/SavedDesignsPanel";
import type {
  DesignProjectDetails,
  DesignProjectSummary,
  AiAccountStatus,
  GenerateDesignRequest,
  ResultTab
} from "./types";
import { buildChatGptPrompt, copyText } from "./utils/exports";
import "./brainstorm.css";

const emptyForm: GenerateDesignRequest = {
  summary: "",
  technology: "",
  cloudHostingTarget: "",
  userCount: "",
  features: ""
};

const exampleForm: GenerateDesignRequest = {
  summary: "Food delivery app for customers, restaurants, and delivery riders",
  technology:
    "React, ASP.NET Core, SQL Server for core data, Redis for cache and queues",
  cloudHostingTarget: "Microsoft Azure",
  userCount: "10,000 users",
  features:
    "Ordering, payment, menu management, delivery tracking, notifications"
};

export function BrainstormPage() {
  const { session } = useSession();
  const [form, setForm] = useState<GenerateDesignRequest>(emptyForm);
  const [designs, setDesigns] = useState<DesignProjectSummary[]>([]);
  const [selectedDesign, setSelectedDesign] =
    useState<DesignProjectDetails | null>(null);
  const [activeTab, setActiveTab] = useState<ResultTab>("overview");
  const [chatGptResult, setChatGptResult] = useState("");
  const [copyStatus, setCopyStatus] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [aiAccount, setAiAccount] = useState<AiAccountStatus | null>(null);
  const [isAccountLoading, setIsAccountLoading] = useState(false);
  const [activeLoginId, setActiveLoginId] = useState<string | null>(null);
  const isAdmin = session?.roles.includes("Admin") ?? false;
  const isAiConnected = aiAccount?.state === "connected";

  const isFormValid = useMemo(
    () =>
      form.summary.trim().length > 0 &&
      form.technology.trim().length > 0 &&
      form.userCount.trim().length > 0 &&
      form.features.trim().length > 0,
    [form]
  );

  useEffect(() => {
    const controller = new AbortController();
    void refreshHistory(controller.signal);
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!isAdmin) return;
    const controller = new AbortController();
    void refreshAiAccount(controller.signal);
    return () => controller.abort();
  }, [isAdmin]);

  async function refreshAiAccount(signal?: AbortSignal): Promise<void> {
    setIsAccountLoading(true);
    try {
      setAiAccount(await getAiAccountStatus(signal));
    } catch (caught) {
      if (caught instanceof DOMException && caught.name === "AbortError") return;
      setError(messageFrom(caught, "Could not read the ChatGPT connection."));
    } finally {
      setIsAccountLoading(false);
    }
  }

  async function handleConnectAccount(): Promise<void> {
    if (!session || !isAdmin || isAccountLoading) return;
    const popup = window.open(
      "about:blank",
      "wfhmonitor-chatgpt-oauth",
      "width=720,height=820"
    );
    setIsAccountLoading(true);
    setError(null);
    try {
      const login = await connectAiAccount(session.antiForgeryToken);
      setActiveLoginId(login.loginId);
      if (popup) popup.location.href = login.authUrl;
      else window.location.assign(login.authUrl);

      for (let attempt = 0; attempt < 300; attempt += 1) {
        await new Promise((resolve) => window.setTimeout(resolve, 1000));
        const status = await getAiLoginStatus(login.loginId);
        if (status.state === "pending") continue;
        setActiveLoginId(null);
        if (status.state !== "completed")
          throw new Error(status.message ?? "ChatGPT connection was not completed.");
        if (popup && !popup.closed) popup.close();
        setAiAccount(await getAiAccountStatus());
        return;
      }
      throw new Error("ChatGPT connection timed out. You can try connecting again.");
    } catch (caught) {
      if (popup && !popup.closed) popup.close();
      setError(messageFrom(caught, "Could not connect ChatGPT."));
    } finally {
      setIsAccountLoading(false);
    }
  }

  async function handleCancelAccountLogin(): Promise<void> {
    if (!session || !activeLoginId) return;
    await cancelAiLogin(activeLoginId, session.antiForgeryToken);
    setActiveLoginId(null);
    setIsAccountLoading(false);
  }

  async function handleLogoutAccount(): Promise<void> {
    if (!session) return;
    setIsAccountLoading(true);
    setError(null);
    try {
      await logoutAiAccount(session.antiForgeryToken);
      setAiAccount(await getAiAccountStatus());
    } catch (caught) {
      setError(messageFrom(caught, "Could not disconnect ChatGPT."));
    } finally {
      setIsAccountLoading(false);
    }
  }

  async function refreshHistory(signal?: AbortSignal): Promise<void> {
    setIsLoadingHistory(true);
    try {
      setDesigns(await listDesigns(signal));
    } catch (caught) {
      if (caught instanceof DOMException && caught.name === "AbortError") return;
      setError(messageFrom(caught, "Could not load saved blueprints."));
    } finally {
      setIsLoadingHistory(false);
    }
  }

  async function handleGenerate(): Promise<void> {
    if (!isFormValid || isGenerating || !session || !isAdmin || !isAiConnected) return;
    setIsGenerating(true);
    setError(null);
    try {
      const design = await generateDesign(form, session.antiForgeryToken);
      setSelectedDesign(design);
      setActiveTab("overview");
      await refreshHistory();
    } catch (caught) {
      setError(messageFrom(caught, "Could not generate a blueprint."));
    } finally {
      setIsGenerating(false);
    }
  }

  async function handleCopyPrompt(): Promise<void> {
    if (!isFormValid) return;
    try {
      await copyText(buildChatGptPrompt(form));
      setCopyStatus(
        "Prompt copied. Paste it into ChatGPT, then paste its JSON result below."
      );
      window.setTimeout(() => setCopyStatus(null), 5000);
    } catch (caught) {
      setError(messageFrom(caught, "Could not copy the prompt."));
    }
  }

  async function handleImport(): Promise<void> {
    if (
      !isFormValid ||
      !chatGptResult.trim() ||
      isImporting ||
      !session
    ) return;

    setIsImporting(true);
    setError(null);
    try {
      const design = await importChatGptResult(
        { ...form, chatGptResult },
        session.antiForgeryToken
      );
      setSelectedDesign(design);
      setActiveTab("overview");
      setChatGptResult("");
      await refreshHistory();
    } catch (caught) {
      setError(messageFrom(caught, "Could not import the ChatGPT result."));
    } finally {
      setIsImporting(false);
    }
  }

  async function handleLoad(id: number): Promise<void> {
    setError(null);
    try {
      const design = await getDesign(id);
      setSelectedDesign(design);
      setForm({
        summary: design.summary,
        technology: design.technology,
        cloudHostingTarget: design.cloudHostingTarget ?? "",
        userCount: design.userCount,
        features: design.features
      });
      setActiveTab("overview");
    } catch (caught) {
      setError(messageFrom(caught, "Could not open the blueprint."));
    }
  }

  async function handleDelete(id: number): Promise<void> {
    if (!session) return;
    const design = designs.find((item) => item.id === id);
    if (
      !window.confirm(
        `Delete "${design?.title ?? "this blueprint"}"? This cannot be undone.`
      )
    ) return;

    setError(null);
    try {
      await deleteDesign(id, session.antiForgeryToken);
      if (selectedDesign?.id === id) setSelectedDesign(null);
      await refreshHistory();
    } catch (caught) {
      setError(messageFrom(caught, "Could not delete the blueprint."));
    }
  }

  function updateField(
    field: keyof GenerateDesignRequest,
    value: string
  ): void {
    setForm((current) => ({ ...current, [field]: value }));
  }

  return (
    <section className="bsm-page">
      <header className="bsm-page-heading">
        <div>
          <span className="bsm-kicker">Workspace · Brainstorm</span>
          <h1>System Design Studio</h1>
          <p>
            Move from a product idea to an actionable technical blueprint in
            one focused workspace.
          </p>
        </div>
        <div className="bsm-heading-stat">
          <span><i className="bi bi-journals" /></span>
          <div><strong>{designs.length}</strong><small>Saved blueprints</small></div>
        </div>
      </header>

      <div className="bsm-workspace">
        <aside className="bsm-sidebar">
          <BrainstormInputPanel
            form={form}
            chatGptResult={chatGptResult}
            copyStatus={copyStatus}
            error={error}
            isGenerating={isGenerating}
            isImporting={isImporting}
            isFormValid={isFormValid}
            isAdmin={isAdmin}
            aiAccount={aiAccount}
            isAccountLoading={isAccountLoading}
            isAccountLoginPending={activeLoginId !== null}
            onFieldChange={updateField}
            onChatGptResultChange={setChatGptResult}
            onGenerate={() => void handleGenerate()}
            onCopyPrompt={() => void handleCopyPrompt()}
            onImport={() => void handleImport()}
            onUseExample={() => setForm(exampleForm)}
            onClear={() => {
              setForm(emptyForm);
              setChatGptResult("");
              setError(null);
            }}
            onConnectAccount={() => void handleConnectAccount()}
            onCancelAccountLogin={() => void handleCancelAccountLogin()}
            onLogoutAccount={() => void handleLogoutAccount()}
          />
          <SavedDesignsPanel
            designs={designs}
            selectedDesign={selectedDesign}
            isLoading={isLoadingHistory}
            onRefresh={() => void refreshHistory()}
            onLoad={(id) => void handleLoad(id)}
            onDelete={(id) => void handleDelete(id)}
          />
        </aside>
        <BlueprintWorkspace
          design={selectedDesign}
          activeTab={activeTab}
          onTabChange={setActiveTab}
        />
      </div>
    </section>
  );
}

function messageFrom(caught: unknown, fallback: string): string {
  return caught instanceof Error ? caught.message : fallback;
}
