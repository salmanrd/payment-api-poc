(() => {
    const form = document.querySelector("#service-request-form");
    if (!form) return;

    const fees = document.querySelector("#fees");
    const error = document.querySelector("#form-error");
    const errorMessage = document.querySelector("#form-error-message");
    const result = document.querySelector("#service-request-result");
    const submitButton = document.querySelector("#submit-button");
    let feeIndex = 1;

    function updateRemoveButtons() {
        const rows = fees.querySelectorAll("[data-fee]");
        rows.forEach(row => row.querySelector(".remove-fee").hidden = rows.length === 1);
    }

    document.querySelector("#add-fee").addEventListener("click", () => {
        const row = fees.querySelector("[data-fee]").cloneNode(true);
        row.querySelectorAll("input").forEach(input => {
            input.value = "";
            const field = input.name.replace("fee", "").toLowerCase();
            input.id = `fee-${field}-${feeIndex}`;
            row.querySelector(`label[for^=\"fee-${field}-\"]`).htmlFor = input.id;
        });
        fees.appendChild(row);
        feeIndex += 1;
        updateRemoveButtons();
        row.querySelector("input").focus();
    });

    fees.addEventListener("click", event => {
        if (!event.target.matches(".remove-fee")) return;
        event.target.closest("[data-fee]").remove();
        updateRemoveButtons();
    });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        error.hidden = true;
        submitButton.disabled = true;
        submitButton.textContent = "Creating…";

        const data = new FormData(form);
        const rows = fees.querySelectorAll("[data-fee]");
        const payload = {
            callBackUrl: data.get("callBackUrl"),
            caseReference: data.get("caseReference") || null,
            ccdCaseNumber: data.get("ccdCaseNumber"),
            fees: [...rows].map(row => ({
                code: row.querySelector("[name=feeCode]").value,
                version: row.querySelector("[name=feeVersion]").value,
                calculatedAmount: Number(row.querySelector("[name=feeAmount]").value)
            }))
        };

        try {
            const response = await fetch("/service-request", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            const body = await response.json();
            if (!response.ok) {
                const validationErrors = body.errors ? Object.values(body.errors).flat().join(" ") : null;
                throw new Error(validationErrors || body.error || "The service request could not be created.");
            }

            form.hidden = true;
            document.querySelector("#service-request-reference").textContent = body.serviceRequestReference;
            result.hidden = false;
            result.focus();
        } catch (requestError) {
            errorMessage.textContent = requestError.message || "The service request could not be created. Try again.";
            error.hidden = false;
            error.focus();
        } finally {
            submitButton.disabled = false;
            submitButton.textContent = "Create service request";
        }
    });
})();
