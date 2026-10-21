// Helpers for the scan fields. Handheld and USB scanners need none of this: they type the code
// and press Enter like a keyboard. The camera reader uses the browser's BarcodeDetector, which
// Chrome and Edge ship on Android, ChromeOS and macOS; elsewhere the camera button stays hidden.

const wanted = ["ean_13", "ean_8", "upc_a", "upc_e", "code_128", "code_39", "code_93", "itf", "codabar", "qr_code", "data_matrix"];

export async function isCameraSupported() {
    if (!("BarcodeDetector" in window) || !navigator.mediaDevices?.getUserMedia) {
        return false;
    }

    try {
        return (await BarcodeDetector.getSupportedFormats()).length > 0;
    } catch {
        return false;
    }
}

// Scanners end with Enter. The page moves the focus to the next field while that key is still
// being handled, so without this the same Enter would also submit the form that now has focus.
export function guardEnter(input) {
    input?.addEventListener("keydown", e => {
        if (e.key === "Enter") {
            e.preventDefault();
        }
    });
}

export function selectAll(input) {
    input?.select();
}

// Short beep and vibration: a high one when the scan is accepted, a low double one when not,
// so the operator doesn't need to look at the screen.
let audio;
export function signal(ok) {
    try {
        navigator.vibrate?.(ok ? 40 : [80, 60, 80]);
        audio ??= new (window.AudioContext || window.webkitAudioContext)();
        const beep = (at, frequency) => {
            const oscillator = audio.createOscillator();
            const gain = audio.createGain();
            oscillator.frequency.value = frequency;
            gain.gain.value = 0.08;
            oscillator.connect(gain).connect(audio.destination);
            oscillator.start(audio.currentTime + at);
            oscillator.stop(audio.currentTime + at + 0.09);
        };
        if (ok) {
            beep(0, 1760);
        } else {
            beep(0, 220);
            beep(0.15, 220);
        }
    } catch {
        // Sound and vibration are a nice-to-have.
    }
}

// Opens the rear camera over the page and resolves with the first code read, or null if the
// user cancels.
export async function scanWithCamera(title) {
    const supported = await BarcodeDetector.getSupportedFormats();
    const detector = new BarcodeDetector({ formats: wanted.filter(f => supported.includes(f)) });
    const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: "environment" } }, audio: false });

    const overlay = document.createElement("div");
    overlay.className = "scan-overlay";
    overlay.setAttribute("role", "dialog");
    overlay.setAttribute("aria-modal", "true");
    overlay.innerHTML = `
        <div class="scan-dialog">
            <p class="scan-title"></p>
            <div class="scan-viewport"><video playsinline muted></video><span class="scan-aim"></span></div>
            <button type="button" class="btn btn-block">Cancelar</button>
        </div>`;
    overlay.querySelector(".scan-title").textContent = title || "Apunta al código de barras";
    document.body.appendChild(overlay);

    const video = overlay.querySelector("video");
    video.srcObject = stream;
    await video.play();

    return new Promise(resolve => {
        let done = false;
        const finish = value => {
            if (done) return;
            done = true;
            stream.getTracks().forEach(track => track.stop());
            overlay.remove();
            document.removeEventListener("keydown", onKey);
            resolve(value);
        };
        const onKey = e => { if (e.key === "Escape") finish(null); };
        document.addEventListener("keydown", onKey);
        overlay.querySelector("button").addEventListener("click", () => finish(null));

        const tick = async () => {
            if (done) return;
            try {
                const codes = await detector.detect(video);
                if (codes.length > 0) {
                    finish(codes[0].rawValue);
                    return;
                }
            } catch {
                // The frame wasn't ready; try the next one.
            }
            setTimeout(tick, 120);
        };
        tick();
    });
}
