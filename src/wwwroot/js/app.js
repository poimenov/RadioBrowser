let resizeTimeout; 

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


function playAudio(isPlaying, src) {
    var player = document.getElementById("player");
    if (player && src)
    {
        if (player.src != src)
            player.src = src;
        if (isPlaying) {
            player.play();        
        } else {
            player.pause();
        }
    }        
}

function setVolume(volume) {
    var player = document.getElementById("player");
    player.volume = volume;
}
