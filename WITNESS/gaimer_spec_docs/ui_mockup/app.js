/**
 * WITNESS DESKTOP - UI MOCKUP
 * Interactive prototype with mock data
 */

// ============================================
// MOCK DATA
// ============================================

// Game library with chess flag for filtering
const gameLibrary = [
    { id: 1, name: 'Chess.com', subtitle: 'Play Chess Online', icon: '♟️', isChess: true },
    { id: 2, name: 'Lichess', subtitle: 'Free Online Chess', icon: '♔', isChess: true },
    { id: 3, name: 'League of Legends', subtitle: 'Summoner\'s Rift', icon: '⚔️', isChess: false },
    { id: 4, name: 'Valorant', subtitle: 'Competitive Match', icon: '🎯', isChess: false },
    { id: 5, name: 'Counter-Strike 2', subtitle: 'Competitive', icon: '💥', isChess: false },
    { id: 6, name: 'Fortnite', subtitle: 'Battle Royale', icon: '🏝️', isChess: false },
    { id: 7, name: 'Minecraft', subtitle: 'Survival World', icon: '⛏️', isChess: false },
    { id: 8, name: 'Apex Legends', subtitle: 'Ranked Match', icon: '🔥', isChess: false },
    { id: 9, name: 'Dota 2', subtitle: 'All Pick', icon: '🛡️', isChess: false },
    { id: 10, name: 'Overwatch 2', subtitle: 'Quick Play', icon: '🦸', isChess: false },
    { id: 11, name: 'ChessBase', subtitle: 'Analysis Board', icon: '📊', isChess: true },
    { id: 12, name: 'Chess24', subtitle: 'Live Tournament', icon: '🏆', isChess: true },
];

const agentConfig = {
    general: {
        name: 'General Gaimer',
        shortName: 'General',
        icon: '🎮',
        color: '#a855f7',
        description: 'Your all-around gaming companion'
    },
    chess: {
        name: 'Chess Gaimer',
        shortName: 'Chess',
        icon: '♟️',
        color: '#f59e0b',
        description: 'Specialized chess AI companion'
    }
};

// Sample preview images for games
const mockPreviews = {
    'Chess.com': 'https://placehold.co/800x600/1e293b/f59e0b?text=Chess.com+Game',
    'Lichess': 'https://placehold.co/800x600/1e293b/f59e0b?text=Lichess+Game',
    'ChessBase': 'https://placehold.co/800x600/1e293b/f59e0b?text=ChessBase+Analysis',
    'Chess24': 'https://placehold.co/800x600/1e293b/f59e0b?text=Chess24+Live',
    'League of Legends': 'https://placehold.co/800x600/1e293b/a855f7?text=League+of+Legends',
    'Valorant': 'https://placehold.co/800x600/1e293b/ef4444?text=Valorant+Match',
    'Counter-Strike 2': 'https://placehold.co/800x600/1e293b/f59e0b?text=CS2+Competitive',
    'Fortnite': 'https://placehold.co/800x600/1e293b/3b82f6?text=Fortnite+BR',
    'Minecraft': 'https://placehold.co/800x600/1e293b/22c55e?text=Minecraft+World',
    'Apex Legends': 'https://placehold.co/800x600/1e293b/ef4444?text=Apex+Legends',
    'Dota 2': 'https://placehold.co/800x600/1e293b/ef4444?text=Dota+2+Match',
    'Overwatch 2': 'https://placehold.co/800x600/1e293b/f59e0b?text=Overwatch+2'
};

// ============================================
// STATE
// ============================================

const state = {
    currentScreen: 'agentSelection', // 'agentSelection' | 'dashboard'
    selectedAgent: null, // 'general' | 'chess'
    games: [], // Filtered game list based on agent
    selectedGame: null,
    isConnected: false,
    isConnecting: false,
    inputVolume: 0,
    outputVolume: 0
};

// ============================================
// DOM ELEMENTS (initialized in init())
// ============================================

let elements = {};

// Track if game selector is collapsed (collapsed by default)
let gameSelectorCollapsed = true;

// ============================================
// SCREEN NAVIGATION
// ============================================

function showScreen(screenName) {
    state.currentScreen = screenName;
    
    const minimalView = document.getElementById('minimalView');
    
    elements.agentSelectionScreen.style.display = screenName === 'agentSelection' ? 'flex' : 'none';
    elements.dashboardScreen.style.display = screenName === 'dashboard' ? 'flex' : 'none';
    if (minimalView) minimalView.style.display = 'none';
}

function selectAgent(agentType) {
    state.selectedAgent = agentType;
    state.selectedGame = null;
    
    // For Chess agent: all games shown but non-chess are disabled
    // For General agent: all games are enabled
    state.games = gameLibrary.map(game => ({
        ...game,
        enabled: agentType === 'general' || game.isChess
    }));
    
    // Update UI with agent theme
    updateAgentUI();
    
    // Show dashboard
    showScreen('dashboard');
    
    // Render game list
    renderGameList();
    updatePreview();
    
    // Collapse game selector by default
    const selector = document.getElementById('gameSelector');
    if (selector && gameSelectorCollapsed) {
        selector.classList.add('collapsed');
    }
    
    showToast(`${agentConfig[agentType].name} activated!`, 'success');
}

function showAgentSelection() {
    // Disconnect if connected
    if (state.isConnected) {
        disconnect();
    }
    
    showScreen('agentSelection');
}

function updateAgentUI() {
    const agent = agentConfig[state.selectedAgent];
    const isChess = state.selectedAgent === 'chess';
    
    // Header badge
    elements.agentBadge.classList.toggle('chess', isChess);
    elements.agentBadgeIcon.textContent = agent.icon;
    elements.agentBadgeText.textContent = agent.name;
    
    // Preview badge
    elements.previewAgentBadge.classList.toggle('chess', isChess);
    elements.previewAgentIcon.textContent = agent.icon;
    elements.previewAgentName.textContent = agent.shortName;
    
    // Footer
    elements.footerAgent.textContent = agent.name;
    elements.footerAgent.classList.toggle('chess', isChess);
    
    // Empty state
    elements.emptyIcon.textContent = agent.icon;
    
    // Preview container border color
    elements.previewContainer.style.borderColor = agent.color;
}

// ============================================
// RENDER FUNCTIONS
// ============================================

function renderGameList() {
    const isChessAgent = state.selectedAgent === 'chess';
    
    elements.appList.innerHTML = state.games.map(game => {
        const isSelected = state.selectedGame?.id === game.id;
        const isDisabled = !game.enabled;
        
        return `
            <div class="app-item ${isSelected ? 'selected' : ''} ${isDisabled ? 'disabled' : ''}" 
                 onclick="${isDisabled ? '' : `selectGame(${game.id})`}"
                 title="${isDisabled ? 'Not available for Chess Gaimer' : game.name}">
                <div class="app-thumbnail ${isDisabled ? 'disabled' : ''}">${game.icon}</div>
                <div class="app-info">
                    <div class="app-name">${game.name}</div>
                    <div class="app-title">${game.subtitle}</div>
                </div>
                ${isDisabled ? '<div class="disabled-badge">🔒</div>' : ''}
                ${game.isChess && isChessAgent ? '<div class="chess-badge">♟️</div>' : ''}
            </div>
        `;
    }).join('');
}

function updateConnectionUI() {
    const badge = elements.connectionBadge;
    const btn = elements.connectBtn;
    const btnText = elements.connectBtnText;
    
    if (!badge || !btn || !btnText) return;
    
    badge.classList.remove('connected', 'connecting');
    btn.classList.remove('connected', 'connecting');
    
    if (state.isConnecting) {
        badge.classList.add('connecting');
        btn.classList.add('connecting');
        badge.querySelector('.status-text').textContent = 'CONNECTING...';
        btnText.textContent = 'CONNECTING...';
    } else if (state.isConnected) {
        badge.classList.add('connected');
        btn.classList.add('connected');
        badge.querySelector('.status-text').textContent = 'ONLINE';
        btnText.textContent = 'DISCONNECT';
    } else {
        badge.querySelector('.status-text').textContent = 'OFFLINE';
        btnText.textContent = 'CONNECT';
    }
}

function updatePreview() {
    const agent = agentConfig[state.selectedAgent];
    
    if (state.selectedGame && state.isConnected) {
        elements.previewEmpty.style.display = 'none';
        elements.previewActive.style.display = 'flex';
        elements.previewImage.src = mockPreviews[state.selectedGame.name] || 
            `https://placehold.co/800x600/1e293b/64748b?text=${encodeURIComponent(state.selectedGame.name)}`;
        elements.hudTarget.textContent = state.selectedGame.name;
    } else if (state.selectedGame) {
        elements.previewEmpty.style.display = 'flex';
        elements.previewActive.style.display = 'none';
        elements.previewEmpty.innerHTML = `
            <div class="empty-icon">${state.selectedGame.icon}</div>
            <div class="empty-text">${state.selectedGame.name}</div>
            <div class="empty-subtext">Ready to connect</div>
        `;
    } else {
        elements.previewEmpty.style.display = 'flex';
        elements.previewActive.style.display = 'none';
        elements.previewEmpty.innerHTML = `
            <div class="empty-icon">${agent?.icon || '🎮'}</div>
            <div class="empty-text">No Game Selected</div>
            <div class="empty-subtext">Choose a game from the dropdown above</div>
        `;
    }
}

// ============================================
// ACTIONS
// ============================================

function selectGame(gameId) {
    const game = state.games.find(g => g.id === gameId);
    if (!game || !game.enabled) return;
    
    state.selectedGame = game;
    renderGameList();
    updatePreview();
    updateSelectedGamePreview();
    
    // Auto-collapse after selection
    if (!gameSelectorCollapsed) {
        toggleGameSelector();
    }
    
    if (state.selectedGame) {
        showToast(`Selected: ${state.selectedGame.name}`, 'info');
    }
}

function showGameSelector() {
    // Expand the game selector if collapsed
    if (gameSelectorCollapsed) {
        toggleGameSelector();
    }
    elements.appList.scrollIntoView({ behavior: 'smooth' });
    elements.appList.style.animation = 'none';
    setTimeout(() => {
        elements.appList.style.animation = 'highlight 1s ease';
    }, 10);
}

function toggleGameSelector() {
    gameSelectorCollapsed = !gameSelectorCollapsed;
    const selector = document.getElementById('gameSelector');
    selector.classList.toggle('collapsed', gameSelectorCollapsed);
    
    // Show selected game preview when collapsed
    updateSelectedGamePreview();
}

function updateSelectedGamePreview() {
    const preview = document.getElementById('selectedGamePreview');
    const iconEl = document.getElementById('selectedGameIcon');
    const nameEl = document.getElementById('selectedGameName');
    
    if (state.selectedGame && gameSelectorCollapsed) {
        preview.style.display = 'flex';
        iconEl.textContent = state.selectedGame.icon;
        nameEl.textContent = state.selectedGame.name;
    } else {
        preview.style.display = 'none';
    }
}

async function toggleConnection() {
    if (state.isConnecting) return;
    
    if (state.isConnected) {
        disconnect();
    } else {
        await connect();
    }
}

async function connect() {
    if (!state.selectedGame) {
        showToast('Please select a game first', 'error');
        return;
    }
    
    state.isConnecting = true;
    updateConnectionUI();
    
    await delay(2000);
    
    state.isConnecting = false;
    state.isConnected = true;
    updateConnectionUI();
    updatePreview();
    
    // Switch to minimal view
    switchToMinimalView();
    startVisualizer();
    startMinimalVisualizer();
    startSlidingPanelDemo();
    
    const agent = agentConfig[state.selectedAgent];
    showToast(`${agent.name} ready for ${state.selectedGame.name}!`, 'success');
}

function disconnect() {
    state.isConnected = false;
    updateConnectionUI();
    updatePreview();
    
    // Stop all visualizers
    stopVisualizer();
    stopMinimalVisualizer();
    
    // Stop sliding panel demo and hide all panels
    stopSlidingPanelDemo();
    hideAllSlidingPanels();
    
    // Stop audio simulation
    if (audioSimulationId) {
        clearInterval(audioSimulationId);
        audioSimulationId = null;
    }
    
    // Reset volume levels
    state.inputVolume = 0;
    state.outputVolume = 0;
    if (elements.inputLevel) elements.inputLevel.textContent = '0%';
    if (elements.outputLevel) elements.outputLevel.textContent = '0%';
    
    // Switch back to main view
    switchToMainView();
    showToast('Disconnected from Gemini', 'info');
}

function switchToMinimalView() {
    const mainView = document.getElementById('dashboardScreen');
    const minimalView = document.getElementById('minimalView');
    const agentScreen = document.getElementById('agentSelectionScreen');
    
    // Update minimal view content
    updateMinimalViewContent();
    
    // Hide everything except minimal
    agentScreen.style.display = 'none';
    mainView.style.display = 'none';
    minimalView.style.display = 'flex';
    minimalView.classList.add('active');
}

function switchToMainView() {
    const mainView = document.getElementById('dashboardScreen');
    const minimalView = document.getElementById('minimalView');
    
    // Show main, hide minimal
    mainView.style.display = 'flex';
    minimalView.style.display = 'none';
    minimalView.classList.remove('active');
}

// Expand back to main view while staying connected
function expandToMainView() {
    const mainView = document.getElementById('dashboardScreen');
    const minimalView = document.getElementById('minimalView');
    
    // Hide minimal, show main
    minimalView.style.display = 'none';
    minimalView.classList.remove('active');
    mainView.style.display = 'flex';
    
    // Stop minimal visualizer
    stopMinimalVisualizer();
    
    // Restart main visualizer if connected
    if (state.isConnected) {
        startVisualizer();
    }
    
    // Keep sliding panel demo running - messages will show in main view panel
    
    showToast('Expanded to full view', 'info');
}

function updateMinimalViewContent() {
    const agent = agentConfig[state.selectedAgent];
    const isChess = state.selectedAgent === 'chess';
    
    // Update agent profile
    const profile = document.getElementById('minimalAgentProfile');
    profile.classList.remove('general', 'chess');
    profile.classList.add(state.selectedAgent);
    document.getElementById('minimalAgentIcon').textContent = agent.icon;
    
    // Update agent name
    const agentNameEl = document.getElementById('minimalAgentName');
    if (agentNameEl) {
        agentNameEl.textContent = agent.name;
        agentNameEl.style.color = agent.color;
    }
    
    // Update game info
    if (state.selectedGame) {
        document.getElementById('minimalGameIcon').textContent = state.selectedGame.icon;
        document.getElementById('minimalGameName').textContent = state.selectedGame.name;
    }
}

function refreshGames() {
    const btn = document.querySelector('.icon-btn-small svg');
    btn.style.animation = 'spin 0.5s ease';
    setTimeout(() => {
        btn.style.animation = '';
    }, 500);
    
    showToast('Game list refreshed', 'info');
}

// ============================================
// AUDIO VISUALIZER
// ============================================

let visualizerAnimationId = null;
let audioSimulationId = null;

function startVisualizer() {
    const canvas = elements.visualizerCanvas;
    const ctx = canvas.getContext('2d');
    
    function resizeCanvas() {
        canvas.width = canvas.offsetWidth * 2;
        canvas.height = canvas.offsetHeight * 2;
        ctx.scale(2, 2);
    }
    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    
    const barCount = 32;
    const bars = new Array(barCount).fill(0);
    
    const agent = agentConfig[state.selectedAgent];
    const isChess = state.selectedAgent === 'chess';
    
    audioSimulationId = setInterval(() => {
        if (Math.random() > 0.7) {
            state.inputVolume = Math.random() * 0.8;
        } else {
            state.inputVolume *= 0.9;
        }
        
        if (Math.random() > 0.5) {
            state.outputVolume = Math.random() * 0.6 + 0.2;
        } else {
            state.outputVolume *= 0.95;
        }
        
        elements.inputLevel.textContent = Math.round(state.inputVolume * 100) + '%';
        elements.outputLevel.textContent = Math.round(state.outputVolume * 100) + '%';
    }, 100);
    
    function draw() {
        const width = canvas.offsetWidth;
        const height = canvas.offsetHeight;
        
        ctx.clearRect(0, 0, width, height);
        
        const barWidth = width / barCount - 2;
        const maxHeight = height - 20;
        
        for (let i = 0; i < barCount; i++) {
            const targetHeight = (state.inputVolume * 0.6 + state.outputVolume * 0.4) * 
                                 (0.3 + Math.random() * 0.7) * maxHeight;
            bars[i] += (targetHeight - bars[i]) * 0.3;
        }
        
        for (let i = 0; i < barCount; i++) {
            const x = i * (barWidth + 2);
            const barHeight = Math.max(4, bars[i]);
            const y = height - barHeight - 10;
            
            // Use agent-specific colors
            const gradient = ctx.createLinearGradient(x, y + barHeight, x, y);
            if (isChess) {
                gradient.addColorStop(0, '#f59e0b');
                gradient.addColorStop(0.5, '#d97706');
                gradient.addColorStop(1, '#fbbf24');
            } else {
                gradient.addColorStop(0, '#a855f7');
                gradient.addColorStop(0.5, '#3b82f6');
                gradient.addColorStop(1, '#06b6d4');
            }
            
            ctx.fillStyle = gradient;
            ctx.beginPath();
            ctx.roundRect(x, y, barWidth, barHeight, 2);
            ctx.fill();
            
            // Reflection
            ctx.fillStyle = isChess ? 'rgba(245, 158, 11, 0.2)' : 'rgba(168, 85, 247, 0.2)';
            ctx.beginPath();
            ctx.roundRect(x, height - 8, barWidth, 4, 1);
            ctx.fill();
        }
        
        visualizerAnimationId = requestAnimationFrame(draw);
    }
    
    draw();
}

function stopVisualizer() {
    if (visualizerAnimationId) {
        cancelAnimationFrame(visualizerAnimationId);
        visualizerAnimationId = null;
    }
    if (audioSimulationId) {
        clearInterval(audioSimulationId);
        audioSimulationId = null;
    }
    
    state.inputVolume = 0;
    state.outputVolume = 0;
    if (elements.inputLevel) elements.inputLevel.textContent = '0%';
    if (elements.outputLevel) elements.outputLevel.textContent = '0%';
    
    // Clear main canvas
    if (elements.visualizerCanvas) {
        const canvas = elements.visualizerCanvas;
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
    }
}

// ============================================
// MINIMAL VIEW VISUALIZER
// ============================================

let minimalVisualizerAnimationId = null;

function startMinimalVisualizer() {
    const canvas = document.getElementById('minimalVisualizerCanvas');
    if (!canvas) return;
    
    const ctx = canvas.getContext('2d');
    
    function resizeCanvas() {
        canvas.width = canvas.offsetWidth * 2;
        canvas.height = canvas.offsetHeight * 2;
        ctx.scale(2, 2);
    }
    resizeCanvas();
    
    const barCount = 40;
    const bars = new Array(barCount).fill(0);
    const isChess = state.selectedAgent === 'chess';
    
    function draw() {
        const width = canvas.offsetWidth;
        const height = canvas.offsetHeight;
        
        ctx.clearRect(0, 0, width, height);
        
        const barWidth = width / barCount - 1;
        const maxHeight = height - 4;
        
        for (let i = 0; i < barCount; i++) {
            const targetHeight = (state.inputVolume * 0.6 + state.outputVolume * 0.4) * 
                                 (0.3 + Math.random() * 0.7) * maxHeight;
            bars[i] += (targetHeight - bars[i]) * 0.3;
        }
        
        for (let i = 0; i < barCount; i++) {
            const x = i * (barWidth + 1);
            const barHeight = Math.max(2, bars[i]);
            const y = height - barHeight - 2;
            
            // Use agent-specific colors
            if (isChess) {
                ctx.fillStyle = '#f59e0b';
            } else {
                ctx.fillStyle = '#a855f7';
            }
            
            ctx.beginPath();
            ctx.roundRect(x, y, barWidth, barHeight, 1);
            ctx.fill();
        }
        
        // Update minimal view levels
        const inputEl = document.getElementById('minimalInputLevel');
        const outputEl = document.getElementById('minimalOutputLevel');
        if (inputEl) inputEl.textContent = Math.round(state.inputVolume * 100) + '%';
        if (outputEl) outputEl.textContent = Math.round(state.outputVolume * 100) + '%';
        
        minimalVisualizerAnimationId = requestAnimationFrame(draw);
    }
    
    draw();
}

function stopMinimalVisualizer() {
    if (minimalVisualizerAnimationId) {
        cancelAnimationFrame(minimalVisualizerAnimationId);
        minimalVisualizerAnimationId = null;
    }
    
    // Clear minimal canvas
    const canvas = document.getElementById('minimalVisualizerCanvas');
    if (canvas) {
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
    }
    
    // Reset minimal volume levels
    const inputEl = document.getElementById('minimalInputLevel');
    const outputEl = document.getElementById('minimalOutputLevel');
    if (inputEl) inputEl.textContent = '0%';
    if (outputEl) outputEl.textContent = '0%';
}

// ============================================
// SLIDING INFO PANEL (Minimal View)
// ============================================

let slidingPanelTimeout = null;

function showSlidingPanel(title, text, imageUrl = null, autoDismiss = 6000) {
    // Show in both minimal and main view panels
    const minPanel = document.getElementById('slidingPanel');
    const mainPanel = document.getElementById('mainSlidingPanel');
    const minimalView = document.getElementById('minimalView');
    
    // Clear any existing timeout
    if (slidingPanelTimeout) {
        clearTimeout(slidingPanelTimeout);
    }
    
    // Longer display time for messages with images
    if (imageUrl) {
        autoDismiss = 10000; // 10 seconds for image messages
    }
    
    // Expand minimal view if showing image
    if (minimalView) {
        if (imageUrl) {
            minimalView.classList.add('showing-image');
        } else {
            minimalView.classList.remove('showing-image');
        }
    }
    
    // Update both panels
    [
        { panel: minPanel, prefix: 'slidingPanel' },
        { panel: mainPanel, prefix: 'mainSlidingPanel' }
    ].forEach(({ panel, prefix }) => {
        if (!panel) return;
        
        const titleEl = document.getElementById(`${prefix}Title`);
        const textEl = document.getElementById(`${prefix}Text`);
        const imageEl = document.getElementById(`${prefix}Image`);
        const progressEl = document.getElementById(`${prefix}Progress`);
        
        // Set content
        if (titleEl) titleEl.textContent = title;
        if (textEl) textEl.textContent = text;
        
        // Handle image
        panel.classList.remove('has-image');
        if (imageUrl && imageEl) {
            imageEl.src = imageUrl;
            imageEl.style.display = 'block';
            panel.classList.add('has-image');
        } else if (imageEl) {
            imageEl.style.display = 'none';
        }
        
        // Reset and show progress bar
        if (progressEl && autoDismiss) {
            progressEl.style.animation = 'none';
            progressEl.offsetHeight; // Trigger reflow
            progressEl.style.animation = `panelProgress ${autoDismiss / 1000}s linear forwards`;
        }
        
        // Show panel with animation
        panel.classList.add('visible');
    });
    
    // Auto-dismiss after timeout
    if (autoDismiss) {
        slidingPanelTimeout = setTimeout(() => {
            hideSlidingPanel();
            hideMainSlidingPanel();
            // Remove expanded state
            if (minimalView) {
                minimalView.classList.remove('showing-image');
            }
        }, autoDismiss);
    }
}

function hideSlidingPanel() {
    const panel = document.getElementById('slidingPanel');
    const minimalView = document.getElementById('minimalView');
    if (panel) {
        panel.classList.remove('visible');
        panel.classList.remove('has-image');
    }
    if (minimalView) {
        minimalView.classList.remove('showing-image');
    }
}

function hideMainSlidingPanel() {
    const panel = document.getElementById('mainSlidingPanel');
    if (panel) {
        panel.classList.remove('visible');
        panel.classList.remove('has-image');
    }
}

function hideAllSlidingPanels() {
    hideSlidingPanel();
    hideMainSlidingPanel();
    const minimalView = document.getElementById('minimalView');
    if (minimalView) {
        minimalView.classList.remove('showing-image');
    }
    if (slidingPanelTimeout) {
        clearTimeout(slidingPanelTimeout);
        slidingPanelTimeout = null;
    }
}

// Demo: Show sliding panel periodically when connected
let slidingPanelDemoInterval = null;

function startSlidingPanelDemo() {
    const sampleImage = 'sampleImage.png';
    
    const messages = [
        { title: 'AI Insight', text: 'Consider controlling the center of the board early in the game. Pawns on e4 and d4 establish strong central presence and give your pieces more mobility.' },
        { title: 'Move Suggestion', text: 'Knight to f3 would develop a piece and control key squares. This is a solid developing move that prepares castling and doesn\'t commit to a specific pawn structure yet.' },
        { title: 'Screen Analysis', text: 'Enemy spotted near the building ahead. They appear to be holding an angle on the doorway. Suggest flanking from the right side.', image: sampleImage },
        { title: 'Position Analysis', text: 'Your king safety looks good. Time to start an attack! Your pieces are well-coordinated and you have a slight space advantage on the kingside. Consider pushing your h-pawn to create weaknesses.' },
        { title: 'Tactical Alert', text: 'Watch out! Movement detected at the warehouse. Multiple hostiles converging on your position from the north. Recommend falling back to cover.', image: sampleImage },
        { title: 'Strategic Overview', text: 'Current zone is closing in 45 seconds. The safe area is to the east. Enemy squad spotted heading to the same location - prepare for engagement.', image: sampleImage },
        { title: 'Endgame Tip', text: 'In rook endgames, activity is everything. Keep your rook active and cut off the enemy king. The Lucena and Philidor positions are essential patterns to know.' }
    ];
    
    // Show first message after 3 seconds (with image)
    setTimeout(() => {
        if (state.isConnected) {
            const imageMessages = messages.filter(m => m.image);
            const msg = imageMessages[Math.floor(Math.random() * imageMessages.length)];
            showSlidingPanel(msg.title, msg.text, msg.image);
        }
    }, 3000);
    
    // Show random messages every 10-15 seconds
    slidingPanelDemoInterval = setInterval(() => {
        if (state.isConnected && Math.random() > 0.3) {
            const msg = messages[Math.floor(Math.random() * messages.length)];
            showSlidingPanel(msg.title, msg.text, msg.image || null);
        }
    }, 10000);
}

function stopSlidingPanelDemo() {
    if (slidingPanelDemoInterval) {
        clearInterval(slidingPanelDemoInterval);
        slidingPanelDemoInterval = null;
    }
    hideSlidingPanel();
}

// ============================================
// TOAST NOTIFICATIONS
// ============================================

function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        <span>${getToastIcon(type)}</span>
        <span>${message}</span>
    `;
    
    elements.toastContainer.appendChild(toast);
    
    setTimeout(() => {
        toast.style.animation = 'slideIn 0.3s ease reverse';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function getToastIcon(type) {
    switch (type) {
        case 'success': return '✓';
        case 'error': return '✕';
        case 'info': return 'ℹ';
        default: return '•';
    }
}

// ============================================
// UTILITIES
// ============================================

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// Add CSS animations
const style = document.createElement('style');
style.textContent = `
    @keyframes spin {
        from { transform: rotate(0deg); }
        to { transform: rotate(360deg); }
    }
    @keyframes highlight {
        0%, 100% { box-shadow: none; }
        50% { box-shadow: 0 0 20px rgba(6, 182, 212, 0.5); }
    }
`;
document.head.appendChild(style);

// ============================================
// INITIALIZATION
// ============================================

function init() {
    // Initialize DOM element references
    elements = {
        // Screens
        agentSelectionScreen: document.getElementById('agentSelectionScreen'),
        dashboardScreen: document.getElementById('dashboardScreen'),
        
        // Dashboard elements
        appList: document.getElementById('appList'),
        connectionBadge: document.getElementById('connectionBadge'),
        connectBtn: document.getElementById('connectBtn'),
        connectBtnText: document.getElementById('connectBtnText'),
        previewEmpty: document.getElementById('previewEmpty'),
        previewActive: document.getElementById('previewActive'),
        previewImage: document.getElementById('previewImage'),
        previewContainer: document.getElementById('previewContainer'),
        hudTarget: document.getElementById('hudTarget'),
        footerAgent: document.getElementById('footerAgent'),
        inputLevel: document.getElementById('inputLevel'),
        outputLevel: document.getElementById('outputLevel'),
        visualizerCanvas: document.getElementById('visualizerCanvas'),
        toastContainer: document.getElementById('toastContainer'),
        
        // Agent badge elements
        agentBadge: document.getElementById('agentBadge'),
        agentBadgeIcon: document.getElementById('agentBadgeIcon'),
        agentBadgeText: document.getElementById('agentBadgeText'),
        previewAgentBadge: document.getElementById('previewAgentBadge'),
        previewAgentIcon: document.getElementById('previewAgentIcon'),
        previewAgentName: document.getElementById('previewAgentName'),
        emptyIcon: document.getElementById('emptyIcon'),
        emptySubtext: document.getElementById('emptySubtext')
    };
    
    // Start on agent selection screen
    showScreen('agentSelection');
}

document.addEventListener('DOMContentLoaded', init);

if (document.readyState !== 'loading') {
    init();
}

// ============================================
// KEYBOARD SHORTCUTS
// ============================================

document.addEventListener('keydown', (e) => {
    // Only handle shortcuts on dashboard
    if (state.currentScreen !== 'dashboard') return;
    
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
        e.preventDefault();
        toggleConnection();
    }
    
    if (e.key === 'Escape') {
        if (state.isConnected) {
            toggleConnection();
        } else {
            showAgentSelection();
        }
    }
    
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        
        // Get only enabled games for navigation
        const enabledGames = state.games.filter(g => g.enabled);
        if (enabledGames.length === 0) return;
        
        const currentIndex = state.selectedGame 
            ? enabledGames.findIndex(g => g.id === state.selectedGame.id)
            : -1;
        
        let newIndex;
        if (e.key === 'ArrowDown') {
            newIndex = currentIndex < enabledGames.length - 1 ? currentIndex + 1 : 0;
        } else {
            newIndex = currentIndex > 0 ? currentIndex - 1 : enabledGames.length - 1;
        }
        
        selectGame(enabledGames[newIndex].id);
    }
});

console.log('🎮 Witness Desktop UI Mockup Loaded');
console.log('');
console.log('Agent Selection Screen:');
console.log('  Click on General Purpose or Chess to start');
console.log('');
console.log('Dashboard Keyboard shortcuts:');
console.log('  Ctrl/Cmd + Enter: Connect/Disconnect');
console.log('  Escape: Disconnect or go back to agent selection');
console.log('  Arrow Up/Down: Navigate apps');
