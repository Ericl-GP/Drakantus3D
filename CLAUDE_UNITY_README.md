# Frost Play — Claude Unity Game Dev System

Este pacote é uma camada especializada para usar Claude no desenvolvimento de jogos 3D em Unity.

## Objetivos
1. Preservar a qualidade do projeto.
2. Evitar alterações destrutivas em cenas, prefabs e assets.
3. Reduzir consumo de contexto/créditos.
4. Fazer análise antes da implementação.
5. Trabalhar em mudanças pequenas e verificáveis.
6. Manter uma memória compacta entre sessões.

## Core
Use as Skills genéricas do pacote anterior junto destas Skills Unity. As Skills Unity não devem ser carregadas todas de uma vez.

## Unity Skills
- unity-project-guardian
- unity-csharp
- unity-scenes-prefabs
- unity-input
- unity-player-controller
- unity-camera
- unity-ai-navigation
- unity-combat
- unity-animation
- unity-ui
- unity-scriptable-data
- unity-optimization
- unity-build-release
- unity-editor-tools
- unity-save-system
- unity-asset-pipeline
- unity-scene-performance

## Context budget
Claude deve:
- ler apenas arquivos relevantes;
- evitar repetir análises;
- não fazer scans completos sem necessidade;
- preferir patches focados;
- testar mudanças;
- registrar apenas decisões duráveis.

## Ordem recomendada
1. Project analysis
2. Feature planning
3. Unity-specific implementation
4. Testing
5. Debugging if necessary
6. Project memory update only for durable decisions


## Version lock

Target Unity Editor: **6000.6.4f1**.

Claude must not silently mix instructions or APIs from unrelated Unity versions. For package-specific behavior, inspect the project's package manifests/lock file.
