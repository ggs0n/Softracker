(function () {
    const overlay = document.getElementById("onboardingOverlay");
    const helpButton = document.getElementById("onboardingStartTourButton");

    if (!overlay || !helpButton) {
        return;
    }

    const stepLabel = document.getElementById("onboardingStepLabel");
    const title = document.getElementById("onboardingTitle");
    const description = document.getElementById("onboardingDescription");
    const statusNote = document.getElementById("onboardingStatusNote");
    const goTarget = document.getElementById("onboardingGoTarget");
    const backButton = document.getElementById("onboardingBack");
    const nextButton = document.getElementById("onboardingNext");
    const dismissButton = document.getElementById("onboardingDismiss");
    const dismissTopButton = document.getElementById("onboardingDismissTop");
    const restartButton = document.getElementById("onboardingRestart");
    const antiForgeryToken = document.querySelector("#onboardingAntiForgeryForm input[name='__RequestVerificationToken']")?.value || "";

    const endpoints = {
        state: "/Onboarding/State",
        dismiss: "/Onboarding/Dismiss",
        restart: "/Onboarding/Restart"
    };

    let state = null;
    let currentStepIndex = 0;
    let highlightedElement = null;

    function toSteps(source) {
        return Array.isArray(source) ? source : [];
    }

    function clearHighlight() {
        if (highlightedElement) {
            highlightedElement.classList.remove("onboarding-highlight-target");
            highlightedElement = null;
        }
    }

    function setHighlight(step) {
        clearHighlight();

        if (!step || !step.targetSelector) {
            return null;
        }

        const target = document.querySelector(step.targetSelector);
        if (!target) {
            return null;
        }

        highlightedElement = target;
        target.classList.add("onboarding-highlight-target");
        target.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
        return target;
    }

    function setStatusMessage(message) {
        if (!statusNote) {
            return;
        }

        if (!message) {
            statusNote.classList.add("d-none");
            statusNote.textContent = "";
            return;
        }

        statusNote.classList.remove("d-none");
        statusNote.textContent = message;
    }

    function updateGoTarget(step, targetFound) {
        if (!goTarget) {
            return;
        }

        if (targetFound || !step?.targetUrl || step.hasAccess === false) {
            goTarget.classList.add("d-none");
            goTarget.removeAttribute("href");
            goTarget.textContent = "";
            return;
        }

        goTarget.classList.remove("d-none");
        goTarget.href = step.targetUrl;
        goTarget.textContent = step.targetLabel || "Go to target page";
    }

    function renderStep() {
        if (!state) {
            return;
        }

        const steps = toSteps(state.steps);
        if (!steps.length) {
            closeTour();
            return;
        }

        currentStepIndex = Math.max(0, Math.min(currentStepIndex, steps.length - 1));
        const step = steps[currentStepIndex];
        const target = setHighlight(step);

        if (stepLabel) {
            stepLabel.textContent = `Step ${currentStepIndex + 1} of ${steps.length}`;
        }

        if (title) {
            title.textContent = step.title || "Onboarding";
        }

        if (description) {
            description.textContent = step.description || "";
        }

        if (step.hasAccess === false) {
            setStatusMessage(step.noAccessMessage || "");
        } else if (step.isComplete) {
            setStatusMessage("Completed");
        } else {
            setStatusMessage("");
        }

        updateGoTarget(step, !!target);

        if (backButton) {
            backButton.disabled = currentStepIndex === 0;
        }

        if (nextButton) {
            const isLastStep = currentStepIndex >= steps.length - 1;
            nextButton.textContent = isLastStep ? "Finish" : "Next";
        }
    }

    function openTour(startIndex) {
        const steps = toSteps(state?.steps);
        if (!steps.length) {
            return;
        }

        currentStepIndex = Math.max(0, Math.min(startIndex, steps.length - 1));
        overlay.classList.remove("d-none");
        overlay.setAttribute("aria-hidden", "false");
        document.body.classList.add("onboarding-open");
        renderStep();
    }

    function closeTour() {
        clearHighlight();
        overlay.classList.add("d-none");
        overlay.setAttribute("aria-hidden", "true");
        document.body.classList.remove("onboarding-open");
    }

    async function getState() {
        try {
            const response = await fetch(endpoints.state, {
                method: "GET",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                cache: "no-store"
            });

            if (!response.ok) {
                return null;
            }

            return await response.json();
        } catch {
            return null;
        }
    }

    async function postWithToken(url, payload) {
        const form = new URLSearchParams();
        form.append("__RequestVerificationToken", antiForgeryToken);

        if (payload) {
            Object.keys(payload).forEach(function (key) {
                const value = payload[key];
                if (value !== undefined && value !== null) {
                    form.append(key, String(value));
                }
            });
        }

        try {
            const response = await fetch(url, {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: form.toString()
            });

            if (!response.ok) {
                return null;
            }

            return await response.json();
        } catch {
            return null;
        }
    }

    function handleNext() {
        const steps = toSteps(state?.steps);
        if (!steps.length) {
            closeTour();
            return;
        }

        if (currentStepIndex >= steps.length - 1) {
            closeTour();
            return;
        }

        currentStepIndex += 1;
        renderStep();
    }

    function handleBack() {
        if (currentStepIndex <= 0) {
            return;
        }

        currentStepIndex -= 1;
        renderStep();
    }

    async function handleDismiss() {
        const step = toSteps(state?.steps)[currentStepIndex];
        const nextState = await postWithToken(endpoints.dismiss, {
            lastSeenStepKey: step?.key || state?.currentStepKey || "add-project"
        });

        if (nextState) {
            state = nextState;
        }

        closeTour();
    }

    async function handleRestart() {
        const nextState = await postWithToken(endpoints.restart, {});
        if (nextState) {
            state = nextState;
        }

        openTour(0);
    }

    if (nextButton) {
        nextButton.addEventListener("click", handleNext);
    }

    if (backButton) {
        backButton.addEventListener("click", handleBack);
    }

    if (dismissButton) {
        dismissButton.addEventListener("click", handleDismiss);
    }

    if (dismissTopButton) {
        dismissTopButton.addEventListener("click", handleDismiss);
    }

    if (restartButton) {
        restartButton.addEventListener("click", handleRestart);
    }

    helpButton.addEventListener("click", function () {
        handleRestart();
    });

    overlay.addEventListener("click", function (event) {
        if (event.target && event.target.classList.contains("onboarding-backdrop")) {
            handleDismiss();
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && !overlay.classList.contains("d-none")) {
            handleDismiss();
        }
    });

    getState().then(function (loadedState) {
        if (!loadedState || loadedState.isEligible !== true) {
            return;
        }

        state = loadedState;
        const autoIndex = Number.isInteger(state.currentStepIndex)
            ? state.currentStepIndex
            : 0;

        if (state.isVisible) {
            openTour(autoIndex);
        }
    });
})();
