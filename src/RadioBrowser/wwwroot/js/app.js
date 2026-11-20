let resizeTimeout; 

function readRadioBrowserSetting(key, fallback) {
    try {
        return localStorage.getItem(key) || fallback;
    } catch (error) {
        console.warn(`Unable to read RadioBrowser setting '${key}' from localStorage.`, error);
        return fallback;
    }
}

function writeRadioBrowserSetting(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch (error) {
        console.warn(`Unable to save RadioBrowser setting '${key}' to localStorage.`, error);
    }
}

function applyRadioBrowserTheme(theme, accentColor) {
    const root = document.documentElement;
    const normalizedTheme = theme === "dark" ? "dark" : "light";
    const normalizedAccent = /^#[0-9a-f]{6}$/i.test(accentColor) ? accentColor : "#036ac4";

    root.dataset.theme = normalizedTheme;
    root.style.colorScheme = normalizedTheme;
    root.style.setProperty("--radio-accent", normalizedAccent);
    root.style.setProperty("--colorBrandBackground", normalizedAccent);
    root.style.setProperty("--colorBrandBackgroundHover", `color-mix(in srgb, ${normalizedAccent} 90%, black)`);
    root.style.setProperty("--colorBrandBackgroundPressed", `color-mix(in srgb, ${normalizedAccent} 80%, black)`);
    root.style.setProperty("--colorBrandForeground1", normalizedAccent);
    root.style.setProperty("--colorBrandForegroundLink", normalizedAccent);
    root.style.setProperty("--colorBrandForegroundLinkHover", `color-mix(in srgb, ${normalizedAccent} 80%, black)`);

    writeRadioBrowserSetting("radioBrowserTheme", normalizedTheme);
    writeRadioBrowserSetting("radioBrowserAccentColor", normalizedAccent);
}

function getRadioBrowserTheme() {
    return readRadioBrowserSetting("radioBrowserTheme", "light");
}

function getRadioBrowserAccentColor() {
    return readRadioBrowserSetting("radioBrowserAccentColor", "#036ac4");
}

applyRadioBrowserTheme(getRadioBrowserTheme(), getRadioBrowserAccentColor());

window.setCallbacks = (elementId, dotNetRef) => {
    const observer = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                dotNetRef.invokeMethodAsync('OnElementVisible');
            }
        });
    });

    const target = document.getElementById(elementId);
    if (target) {
        observer.observe(target);
    }

    
    window.addEventListener("resize", () => {
        clearTimeout(resizeTimeout); 
        resizeTimeout = setTimeout(() => {
            dotNetRef.invokeMethodAsync('OnWindowResize', window.innerWidth, window.innerHeight);
        }, 500);
    });        
};


function playAudio(isPlaying, src, volume) {
    var player = document.getElementById("player");
    if (!player) {
        return;
    }

    player.volume = volume;
    if (src && player.src !== src) {
        player.src = src;
    }

    if (isPlaying) {
        player.play();
    } else {
        player.pause();
    }
}

function setVolume(volume) {
    var player = document.getElementById("player");
    if (!player) {
        throw new Error("Audio player element was not found.");
    }

    const normalizedVolume = Number(volume);
    if (!Number.isFinite(normalizedVolume) || normalizedVolume < 0 || normalizedVolume > 1) {
        throw new RangeError("Volume must be between 0 and 1.");
    }

    player.volume = normalizedVolume;
}
