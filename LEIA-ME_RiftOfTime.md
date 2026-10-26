# Rift Of Time — guia da implementação

Jogo em primeira pessoa (3D, Unity 6000.5.10f1 + URP + Input System).
O código do jogo está em `Assets/FragmentsOfTime/`. Nada do pacote *Ancient Modular Tombs* foi alterado.

## Como começar (3 passos)
1. Abra o projeto na Unity **6000.5.10f1** (a mesma do `ProjectVersion.txt`) e espere a importação.
2. Aparecerá um aviso "As cenas do jogo ainda não foram criadas". Clique em **Construir agora**
   (ou use o menu **Rift Of Time ▸ 1. Construir todas as cenas do jogo**).
   Isso cria 6 cenas em `Assets/Scenes/RiftOfTime/` e coloca todas no Build Settings.
3. Abra `00_Menu` (menu **Rift Of Time ▸ 2. Abrir cena do Menu**) e aperte **Play**.

Qualquer fase também roda isolada (Play direto na cena): o `GameManager` se cria sozinho.

## Controles
| Ação | Tecla |
|---|---|
| Andar / correr / pular | W A S D / Shift / Espaço (controlador original do pacote) |
| Golpear com a adaga | **Clique** (botão esquerdo) |
| Esquivar (dash com invulnerabilidade) | **Q** ou **Ctrl esquerdo** |
| Interagir (papiro, fragmento, máquina) | **E** |
| Lanterna | F |
| Pausar | Esc |
| Pular o prólogo | Espaço |

## Storyboard → cenas → código
| Tela | Cena | O que acontece | Scripts principais |
|---|---|---|---|
| 00 Tela inicial | `00_Menu` | Pirâmide 3D sob céu noturno, lua, estrelas piscando; botões *Iniciar Expedição / Continuar / Opções*; mouse ou teclado; Opções tem volume, tela cheia | `MainMenu`, `StarTwinkle` |
| 01 Ativação e portal | `01_Prologo` | Narração do relatório, explorador se aproxima, sobrecarga, tremor forte, zoom no portal, scanner com "ERRO!", estalo elétrico → silêncio → flash branco → Egito Antigo | `PrologueDirector`, `TimeMachine` |
| 02 Fase 1 | `02_Fase1_TemploPerdido` | Papiro (conta a história), Guardiã I desperta ao entrar no domínio; esquivar/contra-atacar; ao cair, solta o Fragmento I (auto-coleta) | `PhaseController`, `MummyGuardian`, `PapyrusPickup`, `FragmentPickup` |
| 03 Fase 2 | `03_Fase2_RuinasDoDeserto` | Deserto com pirâmide ao fundo, câmara do Faraó (braços cruzados, golpe frontal + giro em área); fragmento coletado com **[E]**; chão se abre → cartela "Fase 3" | `MummyGuardian` (isPharaoh), `TrapdoorFloor` |
| 04 Fase 3 | `04_Fase3_CamaraDoTempo` | Corredor até a grande câmara; ao pegar o 3º fragmento: cronômetro **03:00**, tremor contínuo, círculos tracejados no chão avisam onde a pedra cai; correr de volta à máquina | `CollapseController`, `FallingRock`, `TimeMachine` |
| 05 Final | `05_Final` | Encaixar fragmentos e ativar a máquina → "EXPEDIÇÃO CONCLUÍDA" → portal → fade → créditos (com "Viagem temporal concluída") | `PhaseController`, `CreditsScreen` |

HUD (todas as fases): contador de fragmentos, **barra de vida vertical**, botão/dica "Clique", cronômetro (Fase 3), barra do guardião, legendas e prompt `[E]`.

## O que é provisório (trocar quando o time tiver os assets)
- **Múmias, Faraó, adaga, máquina e fragmentos** são feitos com primitivas 3D. Para usar um modelo próprio de múmia:
  coloque-o como filho do guardião com o nome `Model` — o código para de gerar o visual procedural.
- **Áudio**: os sons do storyboard (gemido, tambores, estalo, batimento, acorde, trilha) são gerados em código
  (`ProceduralAudio`). Troque por arquivos reais nas chamadas de `AudioDirector`.
- Os números de balanceamento ficam nos componentes da cena (vida/dano dos guardiões em `MummyGuardian`,
  duração do colapso em `CollapseController.duration` — 180 s como no storyboard).
- A vida do jogador volta a 100% a cada fase. "Continuar" recomeça na última fase iniciada.

## Estrutura
```
Assets/FragmentsOfTime/
  Scripts/Core     GameManager, ScreenFader, AudioDirector, ProceduralAudio, UIFactory, ...
  Scripts/Player   PlayerHealth, PlayerCombat, PlayerInteractor, CameraShake
  Scripts/Enemies  MummyGuardian, MummyBuilder
  Scripts/World    FragmentPickup, PapyrusPickup, TimeMachine, CollapseController, FallingRock, TrapdoorFloor
  Scripts/Flow     PhaseController, PrologueDirector
  Scripts/UI       HUD, MainMenu, CreditsScreen
  Editor/          RiftOfTimeBuilder (menu "Rift Of Time")
```
As cenas `Intro`, `MaquinaTempo` e `SampleScene` originais estavam vazias e foram mantidas intactas (fora do Build Settings).

## Observação
Se alguma peça modular (parede/porta/tocha) aparecer virada ao contrário, gire o objeto na cena — as cenas são editáveis.
