# Criador de Andares — passo a passo

Ferramenta do **editor da Unity** para criar os andares da Torre sem programar.
Menu: **Drakantus › Criador de Andares**.

## 1. Gerar um andar
1. Abra a janela e fique na aba **Gerar**.
2. **Andar**: número (ex.: 3), nome, subtítulo (pode deixar vazio), dificuldade (1–10) e nível recomendado.
   - O id fica `tower_f3` (ou o que você digitar). Andares 1 e 2 já existem: salvar com `tower_f1`/`tower_f2` **substitui** o original.
3. **Tamanho**: Pequeno / Médio / Grande / Enorme ou Personalizado, e o número de salas.
4. **Bioma**: principal + secundário opcional (mistura). Ex.: *Ruínas* + *Floresta*.
5. **Estrutura**: níveis (0 = plano, 1–3 = salas em alturas diferentes com rampas e plataformas com escada),
   corredores estreitos/largos, sala secreta, porta trancada com alavanca, arenas (portas que fecham).
6. **Conteúdo**: densidade de inimigos, % de elites, mini-chefes (0–4), chefe final, armadilhas escondidas
   e à vista, névoa venenosa, baús comuns/raros, santuário no fim.
7. Clique **GERAR ANDAR COMPLETO**. A cena `Assets/Floors/Andar_03.unity` é criada/aberta com o objeto
   `Andar_03` dentro. O quadro azul embaixo mostra o resumo (salas, inimigos, avisos).
   - Não gostou? **Regerar (outra semente)**. A mesma semente sempre gera o mesmo andar.

## 2. Retocar à mão
- Mova/apague qualquer coisa normalmente na Scene View (tudo fica organizado em pastas: Chao, Paredes,
  Decoracao, Luzes, Armadilhas, Portas_Alavancas, Inimigos, MiniChefes, Chefes, Baus, Escadas...).
- Aba **Retoque**: escolha a categoria e a peça e **clique na Scene View** para colocar.
  **Shift+clique** coloca várias, **R** gira 45°, **Esc** cancela. "Encaixar na grade" alinha pisos, paredes,
  portas e escadas às células de 4 m.
- Inimigos: marque **Elite** e as habilidades (Investida, Escudo, Invocar, Veneno, Teleporte, Fúria, Fogo, Pedras).
- **Ligações**: selecione a FONTE (placa de pressão, alavanca ou gatilho de arena) e, com Ctrl/Cmd, os ALVOS
  (portas, armadilhas, baús) → botão **Ligar seleção**. Selecionando a fonte aparecem linhas amarelas até os alvos.
- Os marcadores (inimigos, baús, entrada, santuário) aparecem como desenhos coloridos com nome — ligue **Gizmos**.

## 3. Salvar e testar
- **Salvar andar**: grava `Assets/Drakantus/Resources/Floors/<id>.prefab` e registra em `Resources/Data/floors.json`.
- **Testar andar (Play)**: salva, abre a cena Drakantus e entra no jogo **direto no andar** com um herói de teste
  no nível recomendado (escolha a classe ao lado do botão). Seu save verdadeiro é copiado antes e devolvido ao sair do Play.
- Aba **Andares**: lista o que o jogo conhece; dá para abrir a cena ou remover um andar da lista.

## Dicas
- Paredes do lado da câmera ficam baixas de propósito (não tampam o herói).
- No máximo 2 luzes com sombra por andar (braseiros do chefe); o resto é sem sombra.
- Limite de ~2500 objetos: se aparecer o aviso, diminua o tamanho ou a decoração.
- Biomas, cores, modelos e partículas ficam em `Resources/Data/biomes.json`; habilidades dos chefes em
  `Resources/Data/bosses.json`. Biomas sem modelos próprios (deserto, neve, cidade destruída) usam modelos
  KayKit tingidos + formas simples (`#duna`, `#cristal`, `#escombro`...). Quando baixar Quaternius Nature,
  Kenney Holiday ou KayKit Dungeon, troque os ids no biomes.json (procure `TODO(modelos)` no código).
