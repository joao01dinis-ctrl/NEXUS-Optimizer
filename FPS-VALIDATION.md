# Conferir resultados com capturas de jogo

A NEXUS 0.7 importa CSVs por fotograma do [PresentMon oficial](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md).
Não inclui capturador, overlay, injeção no jogo, driver ou serviço PresentMon.
Não instala nem executa automaticamente outro programa. O utilizador obtém a captura
separadamente e escolhe o ficheiro em Testes e FPS > Escolher CSV do PresentMon.

O formato suportado exige Application, ProcessID, SwapChainAddress e
**MsBetweenPresents**, o intervalo entre apresentações da aplicação, em milissegundos.
Use a opção de métricas v1 do PresentMon quando a versão exportar outros cabeçalhos.
CPUFrameTime/FrameTime não são tratados como aliases por adivinhação. Um resumo com
FPS calculados também não é aceite. CSV UTF-8, separado por vírgulas, aspas escapadas;
campos com várias linhas não são suportados. Limites: 64 MiB, 250 mil linhas,
256 fluxos e 32 mil caracteres por linha.

Cada combinação aplicação/PID/fluxo de imagem fica separada. Escolha o fluxo do
jogo, sem juntar overlays, launchers ou outras janelas. A importação apresenta os
fluxos com pelo menos dois intervalos válidos. Valores zero/NA são contados como
omitidos; valores negativos/não finitos, colunas duplicadas ou linhas mal formadas
recusam a importação. Pausas positivas válidas não são removidas como outliers.

Método próprio, explícito:

- FPS de apresentação: número de intervalos válidos / soma dos intervalos em segundos.
- 1% low: 1000 dividido pela média do 1% de intervalos mais lentos, arredondado para cima.
- Mediana e P99: percentil nearest rank dos intervalos.
- Quando existe Dropped, é contado mas não excluído dos intervalos de apresentação.

São apresentações da aplicação, não a contagem de imagens efetivamente vistas no
ecrã. Não medem fotogramas gerados, latência de entrada ou a qualidade da ligação.
O SHA-256 identifica os bytes importados; não certifica a autenticidade dos dados.
O ficheiro permanece intacto. Só o resumo escolhido, nome sem caminho completo,
hash e condições declaradas são guardados localmente em SQLite e incluídos no backup.

## Antes e depois

1. Use o mesmo PC, versão de jogo, cena/percurso, resolução, qualidade, driver,
   limite de FPS, VSync e geração de fotogramas. Termine downloads e tarefas variáveis.
2. Recolha pelo menos três capturas por condição, de duração semelhante. Altere só
   uma definição entre as condições e preserve o valor anterior para restauro.
3. Importe e guarde cada captura com as condições exatamente iguais. Indique
   explicitamente as condições; a NEXUS não consegue verificá-las só pelo CSV.
4. Escolha a referência e o novo teste. A comparação recusa a mesma origem/hash,
   aplicação/condições diferentes, menos de 300 intervalos ou 20 segundos, mais de um
   intervalo omitido ou duração diferente em mais de 20%.
5. Compare as repetições, FPS e pausas P99. A comparação de dois ficheiros mostra
   variação, sem atribuir causalidade a uma otimização ou declarar ganho garantido.

O diagnóstico em Memória mede também CPU por processo durante cerca de dois
segundos. O cálculo usa tempo de CPU acumulado, tempo monotónico e número de CPUs
lógicas. Confirma PID/arranque entre amostras, ignora processos inacessíveis ou
terminados e não altera prioridades/memória. Uma app com uso alto de CPU nesta
amostra não está automaticamente provada como causa de uma quebra de FPS.

Os testes de lógica usam dados artificiais identificados como fixtures; não são
resultados de jogos nem prova de melhoria no PC. Consulte VALIDATION.md para o
estado real da execução dos testes e do arranque.

## Repetições na 0.7

Classificar as capturas como Referência ou Otimizado ao guardar. Selecionar três a vinte por grupo no painel Comparação de testes repetidos. O conjunto inteiro exige hashes independentes, mesma aplicação/condições, 20 segundos/300 intervalos, até um omitido e duração máxima/mínima até 1,2. Faixas mínimas/máximas e medianas são calculadas entre testes, não misturando frames de durações diferentes. Faixas sobrepostas ou métricas discordantes produzem resultado inconclusivo/misto. Não são intervalos de confiança estatística e nem faixas separadas provam causalidade. Alternar condições A/B e repetir o mesmo percurso, sem limpar shaders/caches entre testes.

0,1% low usa a média dos 0,1% intervalos mais lentos, arredondada para cima, apenas com pelo menos mil intervalos válidos. Capturas antigas sem esse campo continuam disponíveis, sem valor inventado. Frametime médio é a média dos intervalos; não é latência de entrada. GPU Busy, utilização/temperaturas/frequências GPU/CPU, VRAM e captura ao vivo ainda não são recolhidos pelo FPS Lab.