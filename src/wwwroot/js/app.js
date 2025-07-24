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

    window.addEventListener('resize', function() {
        dotNetRef.invokeMethodAsync('OnWindowResize', window.innerWidth, window.innerHeight);
    });    
};

function playAudio(isPlaying) {
    console.log("Playing audio: " + isPlaying);
    var player = document.getElementById("player");
    if (isPlaying) {
        player.play();
    } else {
        player.pause();
    }
}

function setVolume(volume) {
    var player = document.getElementById("player");
    player.volume = volume;
}
