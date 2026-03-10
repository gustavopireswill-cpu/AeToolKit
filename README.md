# AeToolsKit

Painel CEP para Adobe After Effects com layout vertical inspirado no mockup enviado.

## Funcoes

- Precompor camadas selecionadas.
- Excluir o que estiver selecionado na timeline.
- Criar camera 3D centralizada.
- Organizar o projeto em pastas de forma automatica.
- Criar solido com seletor de cor.
- Limpar os caches disponiveis do After Effects.

## Estrutura

- `CSXS/manifest.xml`: manifesto da extensao.
- `index.html`: painel principal.
- `css/styles.css`: visual do painel.
- `js/main.js`: logica da interface.
- `js/cep.js`: ponte basica com CEP.
- `jsx/hostscript.jsx`: scripts que executam dentro do After Effects.

## Instalacao

1. Execute o instalador Windows como administrador ou clique em instalar e confirme o UAC.
2. O instalador baixa a extensao do GitHub e publica em `%CommonProgramFiles(x86)%\\Adobe\\CEP\\extensions\\AeKitTools`.
3. Se desejar, ele habilita o `PlayerDebugMode` automaticamente.
4. Abra o After Effects e acesse `Janela > Extensoes > AK`.

## Instalador Windows

- O projeto inclui um instalador desktop em `installer/AeKitToolsInstaller`.
- Para gerar um `.exe` unico em `dist/AeKitToolsInstaller`, execute `.\build-installer.ps1`.
- O executavel instala a extensao em `%CommonProgramFiles(x86)%\\Adobe\\CEP\\extensions\\AeKitTools`, baixa o payload do GitHub e pode habilitar o `PlayerDebugMode` automaticamente.

## Observacoes

- O painel adapta a cor da borda dos botoes conforme o tema recebido do host, com sombra aplicada.
- A organizacao do projeto move itens soltos na raiz e itens que ja estejam em pastas gerenciadas pelo proprio painel.
- `Limpar cache` usa a purga de caches exposta por script no After Effects.
