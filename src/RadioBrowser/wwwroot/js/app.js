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
    const isDark = theme === "dark";
    const normalizedTheme = isDark ? "dark" : "light";
    const normalizedAccent = /^#[0-9a-f]{6}$/i.test(accentColor) ? accentColor : "#036ac4";

    const foregroundAccent = isDark
        ? `color-mix(in srgb, ${normalizedAccent} 65%, white)`
        : normalizedAccent;

    const foregroundHover = isDark
        ? `color-mix(in srgb, ${normalizedAccent} 50%, white)`
        : `color-mix(in srgb, ${normalizedAccent} 80%, black)`;

    root.dataset.theme = normalizedTheme;
    root.style.colorScheme = normalizedTheme;

    root.style.setProperty("--radio-accent", normalizedAccent);
    root.style.setProperty("--colorBrandBackground", normalizedAccent);
    root.style.setProperty("--colorBrandBackgroundHover", `color-mix(in srgb, ${normalizedAccent} 90%, black)`);
    root.style.setProperty("--colorBrandBackgroundPressed", `color-mix(in srgb, ${normalizedAccent} 80%, black)`);

    root.style.setProperty("--colorBrandForeground1", foregroundAccent);
    root.style.setProperty("--colorBrandForegroundLink", foregroundAccent);
    root.style.setProperty("--colorBrandForegroundLinkHover", foregroundHover);

    writeRadioBrowserSetting("radioBrowserTheme", normalizedTheme);
    writeRadioBrowserSetting("radioBrowserAccentColor", normalizedAccent);
}


function getRadioBrowserTheme() {
    return readRadioBrowserSetting("radioBrowserTheme", "light");
}

function getRadioBrowserAccentColor() {
    return readRadioBrowserSetting("radioBrowserAccentColor", "#036ac4");
}

function positionSettingsSubmenus() {
    const menu = document.querySelector(".settings-menu-list");
    if (!menu) {
        return;
    }

    const menuRect = menu.getBoundingClientRect();

    for (const submenu of menu.querySelectorAll("fluent-menu-item > fluent-menu-list[popover]")) {
        if (!submenu.matches(":popover-open")) {
            continue;
        }

        const itemRect = submenu.parentElement.getBoundingClientRect();
        const submenuRect = submenu.getBoundingClientRect();
        const top = Math.max(0, Math.min(itemRect.top, window.innerHeight - submenuRect.height));

        submenu.style.position = "fixed";
        submenu.style.inset = "auto";
        submenu.style.left = `${Math.max(0, menuRect.left - submenuRect.width)}px`;
        submenu.style.right = "auto";
        submenu.style.top = `${top}px`;
        submenu.style.bottom = "auto";
        submenu.style.translate = "0 0";
    }
}

applyRadioBrowserTheme(getRadioBrowserTheme(), getRadioBrowserAccentColor());

document.addEventListener("toggle", () => requestAnimationFrame(positionSettingsSubmenus), true);
window.addEventListener("resize", () => requestAnimationFrame(positionSettingsSubmenus));

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
