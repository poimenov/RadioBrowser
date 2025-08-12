function checkUTF8(text) {
  var utf8Text = text;
  try {
      // Try to convert to utf-8
      utf8Text = decodeURIComponent(escape(text));
      // If the conversion succeeds, text is not utf-8
  }catch(e) {
      // This exception means text is utf-8
  }   
  return utf8Text;
}

function updateTrack(e) {    
    var statusBar = document.getElementById("status-bar");
    if (e.target && e.target.length > 0) {
        statusBar.innerText = statusBar.getAttribute("data") + checkUTF8(e.target[0].label);
    } 
    else {
        statusBar.innerText = "";
    }   
}

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
    
    var player = document.getElementById("player");          
    if(player.audioTracks) {
        player.audioTracks.addEventListener("change", (event) => {updateTrack(event) });  
    }

};


function playAudio(isPlaying) {
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
